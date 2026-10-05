using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// "Toplu sertifika oluşturma 0 sertifika üretiyor" uzun süre açık kaldı.
/// Araştırınca kuralın doğru çalıştığı görüldü: yazar sertifikası için
/// kongre katılımının tamamlanmış olması gerekiyor ve üretim veritabanında
/// hiç katılım kaydı yoktu (75 kayda karşılık 0 katılım). Yani 0 sertifika
/// beklenen davranıştı.
///
/// Bu testler o kuralı sabitliyor — kural sessiz olduğu için (uygun değilse
/// metot hiçbir şey yapmadan dönüyor) yanlışlıkla değişirse kimse fark
/// etmez.
///
/// Not: aynı akışın HTTP ucu (/Admin/Certificates/TriggerBulk) bu test
/// koşucusunda doğrulanamıyor; o denetleyicinin yetki politikası test
/// kimliğini kabul etmiyor ve istek 401 dönüyor. Bu yüzden kural servis
/// seviyesinde doğrulanıyor.
/// </summary>
public sealed class CertificateBulkTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    // Her test kendi kongresi ve yazarıyla çalışıyor: aynı sınıftaki testler
    // veritabanını paylaştığı için sabit kimlik kullanılırsa biri diğerinin
    // oluşturduğu sertifikayı görüyor ve test kendini yanıltıyor.
    private readonly string AuthorId = "sertifika-yazar-" + Guid.NewGuid().ToString("N")[..8];
    private readonly Guid TenantId = Guid.NewGuid();
    private readonly Guid ConferenceId = Guid.NewGuid();
    private readonly Guid SubmissionId = Guid.NewGuid();

    public CertificateBulkTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Add(new AppUser
        {
            Id = AuthorId,
            UserName = AuthorId + "@test.local",
            NormalizedUserName = (AuthorId + "@TEST.LOCAL").ToUpperInvariant(),
            Email = AuthorId + "@test.local",
            NormalizedEmail = (AuthorId + "@TEST.LOCAL").ToUpperInvariant(),
            FirstName = "Sertifika",
            LastName = "Testi",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = "sertifika-kurum-" + TenantId.ToString("N")[..8], Name = "Sertifika Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Sertifika Kongresi 2026",
            Slug = "sertifika-kongre-" + ConferenceId.ToString("N")[..8],
            StartDate = DateTime.Today.AddDays(-10),
            EndDate = DateTime.Today.AddDays(-8),
            City = "Ankara",
            Country = "Türkiye"
        });

        db.Submissions.Add(new Submission
        {
            Id = SubmissionId,
            TenantId = TenantId,
            ConferenceId = ConferenceId,
            AuthorId = AuthorId,
            Title = "Kabul Edilmiş Bildiri",
            Abstract = "Özet",
            Keywords = "test",
            Topic = string.Empty,
            PresentationType = "Sözlü Sunum",
            Status = SubmissionStatus.Accepted,
            CreatedDate = DateTime.UtcNow
        });

        db.SaveChanges();
    }

    private async Task<int> SertifikaSayisiAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Certificates.IgnoreQueryFilters()
            .CountAsync(c => c.ConferenceId == ConferenceId && c.UserId == AuthorId);
    }

    private async Task SertifikaDeneAsync()
    {
        using var scope = _factory.Services.CreateScope();

        // Yönetici toplu işlemi slug taşımayan adresten çağrılıyor; orada
        // bağlam global oluyor.
        scope.ServiceProvider.GetRequiredService<TenantContext>().IsGlobalContext = true;

        var service = scope.ServiceProvider.GetRequiredService<ICertificateService>();

        await service.EnsureAuthorCertificateAsync(ConferenceId, AuthorId);
    }

    /// <summary>
    /// Kabul edilmiş bildirisi olsa bile katılım tamamlanmadıkça sertifika
    /// verilmemeli. Üretimde 0 sertifika görülmesinin sebebi buydu.
    /// </summary>
    [Fact]
    public async Task KatilimTamamlanmadikca_SertifikaVerilmiyor()
    {
        await SertifikaDeneAsync();

        var sayi = await SertifikaSayisiAsync();

        _output.WriteLine($"katılım kaydı yok -> sertifika: {sayi}");

        Assert.Equal(0, sayi);
    }

    /// <summary>Katılım tamamlanınca sertifika oluşmalı.</summary>
    [Fact]
    public async Task KatilimTamamlandiginda_SertifikaOlusuyor()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.ConferenceAttendances.Add(new ConferenceAttendance
            {
                ConferenceId = ConferenceId,
                UserId = AuthorId,
                FirstJoinedAt = DateTime.UtcNow.AddHours(-2),
                LastPingAt = DateTime.UtcNow,
                TotalSeconds = 7200,
                RequiredSeconds = 60,
                CompletedAt = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        await SertifikaDeneAsync();

        var sayi = await SertifikaSayisiAsync();

        _output.WriteLine($"katılım tamamlandı -> sertifika: {sayi}");

        Assert.True(sayi > 0, "Katılım tamamlandığı hâlde sertifika oluşmadı.");
    }
}
