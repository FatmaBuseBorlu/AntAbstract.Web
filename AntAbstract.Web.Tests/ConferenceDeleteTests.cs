using System.Net;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kongre silme akışı. Silme geri alınamaz bir işlem; iki davranışı da
/// doğrulamak gerekiyor: boş kongre gerçekten siliniyor mu, ve içinde veri
/// olan kongre yanlışlıkla silinemiyor mu.
/// </summary>
public sealed class ConferenceDeleteTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "silme-kurum";

    private static readonly Guid TenantId =
        new("e5e5e5e5-e5e5-e5e5-e5e5-e5e5e5e5e5e5");

    private static readonly Guid BosConferenceId =
        new("e6e6e6e6-e6e6-e6e6-e6e6-e6e6e6e6e6e6");

    private static readonly Guid DoluConferenceId =
        new("e7e7e7e7-e7e7-e7e7-e7e7-e7e7e7e7e7e7");

    public ConferenceDeleteTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Silme Kurumu" });

        db.Conferences.Add(new Conference
        {
            Id = BosConferenceId,
            TenantId = TenantId,
            Title = "Silinecek Boş Kongre",
            Slug = "silinecek-bos",
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(42)
        });

        db.Conferences.Add(new Conference
        {
            Id = DoluConferenceId,
            TenantId = TenantId,
            Title = "Bildirisi Olan Kongre",
            Slug = "silinemez-dolu",
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(42)
        });

        db.Users.Add(new AppUser
        {
            Id = "silme-yazari",
            UserName = "silme-yazari@antabstract.local",
            NormalizedUserName = "SILME-YAZARI@ANTABSTRACT.LOCAL",
            Email = "silme-yazari@antabstract.local",
            NormalizedEmail = "SILME-YAZARI@ANTABSTRACT.LOCAL",
            FirstName = "Silme",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Submissions.Add(new Submission
        {
            Id = Guid.NewGuid(),
            ConferenceId = DoluConferenceId,
            TenantId = TenantId,
            AuthorId = "silme-yazari",
            Title = "Silmeyi engelleyen bildiri",
            Abstract = "Özet",
            Keywords = "test",
            PresentationType = "Oral",
            Status = SubmissionStatus.New,
            CreatedDate = DateTime.UtcNow
        });

        db.SaveChanges();
    }

    private async Task<string> TokenAsync(string url)
    {
        var page = await _client.GetAsync(url);
        var html = await page.Content.ReadAsStringAsync();

        return Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private async Task<HttpResponseMessage> DeleteAsync(Guid id)
    {
        var token = await TokenAsync($"/{Slug}/Admin/Conferences");

        return await _client.PostAsync(
            $"/{Slug}/Admin/Conferences/Delete",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id.ToString()
            }));
    }

    private async Task<bool> ExistsAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Conferences.IgnoreQueryFilters().AnyAsync(c => c.Id == id);
    }

    /// <summary>Boş kongre silinebilmeli.</summary>
    [Fact]
    public async Task EmptyConference_IsDeleted()
    {
        Assert.True(await ExistsAsync(BosConferenceId), "Kongre başlangıçta yok.");

        var response = await DeleteAsync(BosConferenceId);

        _output.WriteLine($"{(int)response.StatusCode} silme -> {response.Headers.Location}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await ExistsAsync(BosConferenceId), "Kongre silinmedi.");
    }

    /// <summary>
    /// İçinde bildiri olan kongre silinmemeli; aksi hâlde yazarların
    /// gönderdiği çalışmalar tek tıkla yok olurdu.
    /// </summary>
    [Fact]
    public async Task ConferenceWithSubmissions_IsRefused()
    {
        var response = await DeleteAsync(DoluConferenceId);

        _output.WriteLine($"{(int)response.StatusCode} engellenen silme");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await ExistsAsync(DoluConferenceId), "Bildirisi olan kongre silindi!");
    }

    /// <summary>Silme yalnızca POST ile yapılmalı; adres yazarak silinememeli.</summary>
    [Fact]
    public async Task Delete_IsNotReachableByGet()
    {
        var response = await _client.GetAsync($"/{Slug}/Admin/Conferences/Delete?id={DoluConferenceId}");

        _output.WriteLine($"{(int)response.StatusCode} GET ile silme denemesi");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await ExistsAsync(DoluConferenceId), "GET ile silinebiliyor!");
    }
}
