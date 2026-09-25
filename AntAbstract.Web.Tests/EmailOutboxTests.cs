using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>SMTP yerine: gönderilenleri kaydeder, adresinde "fail" geçenlerde hata fırlatır.</summary>
public sealed class FakeEmailService : IEmailService
{
    public ConcurrentBag<string> Sent { get; } = new();

    public Task SendAsync(string toEmail, string subject, string htmlMessage)
    {
        if (toEmail.Contains("fail"))
            throw new InvalidOperationException("SMTP bağlantısı kurulamadı");

        Sent.Add(toEmail);
        return Task.CompletedTask;
    }

    public Task SendTemplatedAsync(string toEmail, string templateKey, Dictionary<string, string> placeholders) =>
        Task.CompletedTask;

    public Task<bool> EnqueueTemplatedAsync(string toEmail, string templateKey, Dictionary<string, string> placeholders) =>
        Task.FromResult(false);
}

/// <summary>
/// E-postalar bellekteki kuyrukta bekliyordu: IIS yeniden başlatınca
/// kayboluyor, SMTP bir an cevap vermezse tekrar denenmiyordu. Artık
/// veritabanındaki giden kutusundan gönderiliyor.
/// </summary>
public sealed class EmailOutboxTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeEmailService _smtp = new();

    public EmailOutboxTests(AuthenticatedTestFactory factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddScoped<IEmailService>(_ => _smtp);

            // Testte göndericiyi elle çalıştırıyoruz; arka plandaki kopyası yarışmasın.
            foreach (var hosted in s.Where(d => d.ImplementationType == typeof(OutboxEmailWorker)).ToList())
                s.Remove(hosted);
        }));
    }

    private OutboxEmailWorker Worker(int maxPerMinute = 30) => new(
        _factory.Services.GetRequiredService<IServiceScopeFactory>(),
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Email:MaxPerMinute"] = maxPerMinute.ToString() })
            .Build(),
        NullLogger<OutboxEmailWorker>.Instance);

    private T Db<T>(Func<AppDbContext, T> query)
    {
        using var scope = _factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private void Enqueue(params string[] to) =>
        _factory.Services.GetRequiredService<IEmailQueue>()
            .EnqueueRange(to.Select(t => new EmailQueueItem(t, "Konu", "<p>İçerik</p>")));

    private EmailOutboxMessage Row(string to) =>
        Db(db => db.EmailOutbox.AsNoTracking().Single(m => m.ToEmail == to));

    [Fact]
    public void Enqueue_WritesToDatabase_SoRestartDoesNotLoseIt()
    {
        Enqueue("kalici@antabstract.local");

        // Yeni kapsam = yeniden başlatılmış uygulamanın gördüğü veri.
        var row = Row("kalici@antabstract.local");
        Assert.Equal(EmailOutboxStatus.Pending, row.Status);
        Assert.Equal("Konu", row.Subject);
    }

    [Fact]
    public async Task Worker_SendsPending_AndMarksSent()
    {
        Enqueue("gonder@antabstract.local");

        await Worker().ProcessBatchAsync(CancellationToken.None);

        Assert.Contains("gonder@antabstract.local", _smtp.Sent);
        var row = Row("gonder@antabstract.local");
        Assert.Equal(EmailOutboxStatus.Sent, row.Status);
        Assert.NotNull(row.SentAt);
        Assert.Null(row.LockedUntil);
    }

    [Fact]
    public async Task Worker_OnSmtpError_RetriesLater_ThenGivesUpAfterMaxAttempts()
    {
        Enqueue("fail-tekrar@antabstract.local");

        await Worker().ProcessBatchAsync(CancellationToken.None);

        var row = Row("fail-tekrar@antabstract.local");
        Assert.Equal(EmailOutboxStatus.Pending, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.True(row.NextAttemptAt > DateTime.UtcNow.AddSeconds(30), "Hemen tekrar denenmemeli.");
        Assert.Contains("SMTP", row.LastError);

        // Son deneme: zamanı gelmiş ve 4 kez denenmiş.
        Db(db => db.EmailOutbox.Where(m => m.Id == row.Id).ExecuteUpdate(s => s
            .SetProperty(m => m.Attempts, OutboxEmailWorker.MaxAttempts - 1)
            .SetProperty(m => m.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1))));

        await Worker().ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(EmailOutboxStatus.Failed, Row("fail-tekrar@antabstract.local").Status);
    }

    [Fact]
    public async Task Worker_SkipsRowLockedByAnotherProcess()
    {
        Enqueue("kilitli@antabstract.local");
        Db(db => db.EmailOutbox.Where(m => m.ToEmail == "kilitli@antabstract.local")
            .ExecuteUpdate(s => s.SetProperty(m => m.LockedUntil, DateTime.UtcNow.AddMinutes(3))));

        await Worker().ProcessBatchAsync(CancellationToken.None);

        Assert.DoesNotContain("kilitli@antabstract.local", _smtp.Sent);
        Assert.Equal(EmailOutboxStatus.Pending, Row("kilitli@antabstract.local").Status);
    }

    [Fact]
    public async Task Worker_RespectsPerMinuteLimit()
    {
        var batch = Enumerable.Range(1, 5).Select(i => $"limit-{i}@antabstract.local").ToArray();
        Enqueue(batch);

        var worker = Worker(maxPerMinute: 2);
        await worker.ProcessBatchAsync(CancellationToken.None);
        await worker.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(2, batch.Count(b => _smtp.Sent.Contains(b)));
    }

    [Fact]
    public async Task SuperAdmin_CanRequeueFailedEmails()
    {
        Enqueue("fail-panel@antabstract.local");
        Db(db => db.EmailOutbox.Where(m => m.ToEmail == "fail-panel@antabstract.local")
            .ExecuteUpdate(s => s.SetProperty(m => m.Status, EmailOutboxStatus.Failed)
                                 .SetProperty(m => m.Attempts, OutboxEmailWorker.MaxAttempts)));

        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Admin/Health");
        Assert.Contains("RetryFailedEmails", page);

        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var response = await client.PostAsync("/Admin/Health/RetryFailedEmails",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var row = Row("fail-panel@antabstract.local");
        Assert.Equal(EmailOutboxStatus.Pending, row.Status);
        Assert.Equal(0, row.Attempts);
    }
}
