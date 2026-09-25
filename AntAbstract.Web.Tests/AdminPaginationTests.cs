using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Katılım ve sertifika listeleri kongrenin tamamını tek sayfada basıyordu;
/// sertifikalar ayrıca 300'de sessizce kesiliyordu. 60 kayıtlık bir kongrede
/// 50'lik sayfalar, sayfadan bağımsız özet sayıları ve filtreyi koruyan
/// bağlantılar doğrulanıyor.
/// </summary>
public sealed class AdminPaginationTests : IClassFixture<AuthenticatedTestFactory>
{
    private const string Slug = "sayfa-kurum";
    private const int Count = 60;

    private static readonly Guid TenantId = new("b1b1b1b1-0000-0000-0000-0000000000b1");
    private static readonly Guid ConferenceId = new("b2b2b2b2-0000-0000-0000-0000000000b2");

    private readonly HttpClient _client;

    public AdminPaginationTests(AuthenticatedTestFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
            return;

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Sayfa Kurumu" });
        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Sayfalama Kongresi",
            Slug = "sayfalama-kongresi",
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(12)
        });

        var typeId = Guid.NewGuid();
        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = typeId,
            ConferenceId = ConferenceId,
            Name = "Dinleyici",
            Description = "Test",
            Price = 0,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Listener"
        });

        for (var i = 1; i <= Count; i++)
        {
            var userId = $"sayfa-{i:000}";
            db.Users.Add(new AppUser
            {
                Id = userId,
                UserName = $"{userId}@antabstract.local",
                NormalizedUserName = $"{userId}@ANTABSTRACT.LOCAL".ToUpperInvariant(),
                Email = $"{userId}@antabstract.local",
                NormalizedEmail = $"{userId}@antabstract.local".ToUpperInvariant(),
                FirstName = "Katılımcı",
                LastName = $"Soyad{i:000}",
                SecurityStamp = Guid.NewGuid().ToString()
            });

            db.Registrations.Add(new Registration
            {
                AppUserId = userId,
                ConferenceId = ConferenceId,
                RegistrationTypeId = typeId,
                IsPaid = true,
                // İlk 5 kişi giriş yaptı: özet kartı sayfaya değil kongreye bakmalı.
                CheckedInAt = i <= 5 ? DateTime.UtcNow : null
            });

            db.Certificates.Add(new Certificate
            {
                ConferenceId = ConferenceId,
                UserId = userId,
                Type = CertificateType.Author,
                EligibleAt = DateTime.UtcNow.AddMinutes(-i),
                FilePath = i % 2 == 0 ? $"/certs/{i}.pdf" : null
            });
        }

        db.SaveChanges();
    }

    private async Task<string> GetAsync(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Attendance_ShowsFiftyPerPage_WithWholeConferenceTotals()
    {
        var first = await GetAsync($"/{Slug}/Admin/Attendance?conferenceId={ConferenceId}");
        var second = await GetAsync($"/{Slug}/Admin/Attendance?conferenceId={ConferenceId}&page=2");

        Assert.Contains("Soyad050", first);
        Assert.DoesNotContain("Soyad051", first);
        Assert.Contains("Soyad051", second);
        Assert.Contains("Soyad060", second);
        Assert.DoesNotContain("Soyad050", second);

        Assert.Contains("Toplam 60 kayıt", first);
        Assert.Contains($"conferenceId={ConferenceId}&page=2", first);
        Assert.Contains(">60<", first.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
        Assert.Contains(">55<", first.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
    }

    [Fact]
    public async Task Attendance_SearchFindsPersonBeyondFirstPage()
    {
        var html = await GetAsync($"/{Slug}/Admin/Attendance?conferenceId={ConferenceId}&q=Soyad057");

        Assert.Contains("Soyad057", html);
        Assert.DoesNotContain("Soyad001", html);
        Assert.Contains("Toplam 1 kayıt", html);
    }

    [Fact]
    public async Task Certificates_NoLongerTruncated_AndStatsCoverAllPages()
    {
        var first = await GetAsync($"/{Slug}/Admin/Certificates?conferenceId={ConferenceId}");
        var second = await GetAsync($"/{Slug}/Admin/Certificates?conferenceId={ConferenceId}&page=2");

        Assert.Contains("Toplam 60 kayıt", first);
        Assert.Contains("51–60", second);
        Assert.Contains("sayfa-060@antabstract.local", second);
        Assert.DoesNotContain("sayfa-060@antabstract.local", first);
    }

    [Fact]
    public async Task Pager_OutOfRangePage_FallsBackToLastPage()
    {
        var html = await GetAsync($"/{Slug}/Admin/Attendance?conferenceId={ConferenceId}&page=99");

        Assert.Contains("Soyad060", html);
        Assert.Contains("51–60", html);
    }
}
