using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kongre sitesindeki "Bildiri Gönderimi" rozeti yalnızca IsSubmissionOpen
/// ayarına bakıyordu. Sunucu ise son tarih geçtiyse gönderimi kapatıyor ve
/// kullanıcıyı kongre anasayfasına geri atıyordu. Sonuç: sitede "Aktif"
/// yazarken buton çalışmıyordu.
/// </summary>
public sealed class SubmissionDeadlineBadgeTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string ExpiredSlug = "suresi-dolmus-kongre";
    private const string OpenSlug = "suresi-devam-eden-kongre";

    private static readonly Guid TenantId =
        new("d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1");

    private static readonly Guid ExpiredConferenceId =
        new("d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2");

    private static readonly Guid OpenConferenceId =
        new("d3d3d3d3-d3d3-d3d3-d3d3-d3d3d3d3d3d3");

    private const string AuthorId = "rozet-yazari";

    public SubmissionDeadlineBadgeTests(
        AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AuthorId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = "rozet-kurum",
            Name = "Rozet Kurumu"
        });

        // Gönderim "açık" ama özet son tarihi GEÇMİŞ
        db.Users.Add(new AppUser
        {
            Id = AuthorId,
            UserName = AuthorId + "@antabstract.local",
            NormalizedUserName = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            Email = AuthorId + "@antabstract.local",
            NormalizedEmail = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            FirstName = "Rozet",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Conferences.Add(new Conference
        {
            Id = ExpiredConferenceId,
            TenantId = TenantId,
            Title = "Süresi Dolmuş Kongre",
            Slug = ExpiredSlug,
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(42),
            IsSubmissionOpen = true,
            AbstractSubmissionDeadline = DateTime.UtcNow.AddDays(-1)
        });

        // Gönderim açık ve son tarih İLERİDE
        db.Conferences.Add(new Conference
        {
            Id = OpenConferenceId,
            TenantId = TenantId,
            Title = "Süresi Devam Eden Kongre",
            Slug = OpenSlug,
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(42),
            IsSubmissionOpen = true,
            AbstractSubmissionDeadline = DateTime.UtcNow.AddDays(30)
        });

        // Bildiri gönder butonu yalnızca kayıtlı kullanıcıya gösteriliyor;
        // rozeti ölçebilmek için ikisine de kayıt gerekiyor.
        foreach (var conferenceId in new[] { ExpiredConferenceId, OpenConferenceId })
        {
            var typeId = Guid.NewGuid();

            db.RegistrationTypes.Add(new RegistrationType
            {
                Id = typeId,
                ConferenceId = conferenceId,
                Name = "Akademisyen",
                Description = "Test",
                Price = 0,
                Currency = "TRY",
                IsActive = true,
                RoleName = "Author"
            });

            db.Registrations.Add(new Registration
            {
                Id = Guid.NewGuid(),
                AppUserId = AuthorId,
                ConferenceId = conferenceId,
                RegistrationTypeId = typeId,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,
                Amount = 0
            });
        }

        db.SaveChanges();
    }

    private async Task<string> PageAsync(string slug)
    {
        var response = await _client.GetAsync($"/{slug}");

        _output.WriteLine($"{(int)response.StatusCode} /{slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return System.Net.WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Son tarih geçmişse rozet "Aktif" dememeli — buton zaten çalışmıyor.
    /// </summary>
    [Fact]
    public async Task ExpiredDeadline_DoesNotShowSubmissionAsActive()
    {
        var html = await PageAsync(ExpiredSlug);

        var submitLinks = System.Text.RegularExpressions.Regex.Matches(
            html, @"/[\w-]+/submit-abstract").Count;

        _output.WriteLine($"süresi dolmuş kongrede bildiri gönder bağlantısı: {submitLinks}");

        Assert.Equal(0, submitLinks);
    }

    /// <summary>
    /// Süre devam ederken davranış değişmemeli — çalışan durumu bozmadığımızı
    /// doğrular.
    /// </summary>
    [Fact]
    public async Task ActiveDeadline_StillOffersSubmission()
    {
        var html = await PageAsync(OpenSlug);

        var submitLinks = System.Text.RegularExpressions.Regex.Matches(
            html, @"/[\w-]+/submit-abstract").Count;

        _output.WriteLine($"süresi devam eden kongrede bildiri gönder bağlantısı: {submitLinks}");

        Assert.True(submitLinks > 0, "Süre devam ederken bildiri gönderme sunulmalı.");
    }
}
