using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Yazar panelindeki bildiri gönderme bölümü.
///
/// İki ayrı sorun vardı:
/// 1. Özet son tarihi, yalnızca tam metin son tarihi TANIMSIZKEN uygulanıyordu.
///    Kongrelerin normal kurulumunda iki tarih de dolu olduğu için özet süresi
///    hiç işlemiyor, süre bittikten sonra da yeni bildiri gönderilebiliyordu.
/// 2. "Başvurular" bölüm başlığı yalnızca dinleyiciye basılıyordu; yazarın
///    bildiri bağlantıları başlıksız kalıp "Genel" bölümüne yapışıyordu.
/// </summary>
public sealed class AuthorSubmissionMenuTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "sure-kurum";
    private const string AuthorId = "sure-yazari";

    private const string ExpiredAbstractSlug = "ozet-suresi-dolmus";
    private const string OpenSlug = "sure-devam-eden";
    private const string OnlyFullTextSlug = "sadece-tam-metin";

    private static readonly Guid TenantId =
        new("b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1");

    private static readonly Guid ExpiredAbstractConferenceId =
        new("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");

    private static readonly Guid OpenConferenceId =
        new("b3b3b3b3-b3b3-b3b3-b3b3-b3b3b3b3b3b3");

    private static readonly Guid OnlyFullTextConferenceId =
        new("b4b4b4b4-b4b4-b4b4-b4b4-b4b4b4b4b4b4");

    public AuthorSubmissionMenuTests(
        AuthenticatedTestFactory factory,
        ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AuthorId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = AuthorId,
            UserName = AuthorId + "@antabstract.local",
            NormalizedUserName = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            Email = AuthorId + "@antabstract.local",
            NormalizedEmail = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            FirstName = "Süre",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Süre Kurumu" });

        // Kongrelerin normal kurulumu: iki tarih de dolu, özet süresi bitmiş,
        // tam metin süresi devam ediyor.
        AddConference(
            db,
            ExpiredAbstractConferenceId,
            "Özet Süresi Dolmuş Kongre",
            ExpiredAbstractSlug,
            abstractDeadline: DateTime.UtcNow.AddDays(-2),
            fullTextDeadline: DateTime.UtcNow.AddDays(30));

        AddConference(
            db,
            OpenConferenceId,
            "Süresi Devam Eden Kongre",
            OpenSlug,
            abstractDeadline: DateTime.UtcNow.AddDays(20),
            fullTextDeadline: DateTime.UtcNow.AddDays(50));

        // Özet tarihi girilmemiş; tek sınır tam metin tarihi ve o da geçmiş.
        AddConference(
            db,
            OnlyFullTextConferenceId,
            "Sadece Tam Metin Tarihli Kongre",
            OnlyFullTextSlug,
            abstractDeadline: null,
            fullTextDeadline: DateTime.UtcNow.AddDays(-3));

        db.SaveChanges();
    }

    private static void AddConference(
        AppDbContext db,
        Guid conferenceId,
        string title,
        string slug,
        DateTime? abstractDeadline,
        DateTime? fullTextDeadline)
    {
        db.Conferences.Add(new Conference
        {
            Id = conferenceId,
            TenantId = TenantId,
            Title = title,
            Slug = slug,
            StartDate = DateTime.Today.AddDays(60),
            EndDate = DateTime.Today.AddDays(62),
            IsSubmissionOpen = true,
            AbstractSubmissionDeadline = abstractDeadline,
            FullTextSubmissionDeadline = fullTextDeadline
        });

        // Kayıtsız yazar zaten kayıt sayfasına yönlendiriliyor; süre kontrolünü
        // ölçebilmek için kaydı ekliyoruz.
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

    /// <summary>
    /// Bildiri gönderme adresi önce kurum slug'ına yönleniyor; asıl sonucu
    /// görmek için zinciri takip ediyoruz.
    /// </summary>
    private async Task<HttpResponseMessage> FollowAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var hops = 0;

        while ((int)response.StatusCode is >= 300 and < 400 &&
               response.Headers.Location != null &&
               hops++ < 6)
        {
            url = response.Headers.Location.ToString();
            response = await _client.GetAsync(url);
        }

        _output.WriteLine($"{(int)response.StatusCode} {url}");

        return response;
    }

    private static bool HasSubmissionForm(string html)
    {
        return html.Contains("name=\"Title\"", StringComparison.Ordinal) &&
               html.Contains("name=\"AbstractText\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// Asıl hata: iki tarih birlikte tanımlıyken özet süresi hiç işlemiyordu.
    /// </summary>
    [Fact]
    public async Task SubmissionForm_IsClosed_WhenAbstractDeadlinePassed()
    {
        var response = await FollowAsync($"/{ExpiredAbstractSlug}/submit-abstract");

        var html = await response.Content.ReadAsStringAsync();

        Assert.False(
            HasSubmissionForm(html),
            "Özet gönderim süresi dolmuşken bildiri formu hâlâ açılıyor.");
    }

    /// <summary>Özet tarihi girilmemişse tam metin tarihi tek sınır kalmalı.</summary>
    [Fact]
    public async Task SubmissionForm_IsClosed_WhenOnlyFullTextDeadlineSetAndPassed()
    {
        var response = await FollowAsync($"/{OnlyFullTextSlug}/submit-abstract");

        var html = await response.Content.ReadAsStringAsync();

        Assert.False(
            HasSubmissionForm(html),
            "Tek sınır olan tam metin süresi dolmuşken form açılıyor.");
    }

    /// <summary>Süre devam ederken davranış değişmemeli.</summary>
    [Fact]
    public async Task SubmissionForm_Opens_WhileDeadlineRemains()
    {
        var response = await FollowAsync($"/{OpenSlug}/submit-abstract");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.True(
            HasSubmissionForm(html),
            "Süre devam ederken bildiri formu açılmıyor.");
    }

    /// <summary>
    /// Menüde bildiri bağlantılarının hemen üstünde bir bölüm başlığı olmalı;
    /// başlık yalnızca dinleyiciye basıldığı için yazarda bağlantılar "Genel"
    /// bölümüne yapışık duruyordu.
    /// </summary>
    [Fact]
    public async Task Menu_ShowsSectionLabel_AboveSubmissionLinks()
    {
        var response = await FollowAsync($"/{OpenSlug}/my-submissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        var linkIndex = html.IndexOf("/submit-abstract\"", StringComparison.Ordinal);

        Assert.True(linkIndex > 0, "Bildiri Gönder bağlantısı menüde yok.");

        var before = html[..linkIndex];

        var labelIndex = before.LastIndexOf("menu-label", StringComparison.Ordinal);
        var previousLinkIndex = before.LastIndexOf("nav-link", StringComparison.Ordinal);

        _output.WriteLine($"başlık {labelIndex}, önceki bağlantı {previousLinkIndex}");

        Assert.True(
            labelIndex > previousLinkIndex,
            "Bildiri bağlantılarının üstünde bölüm başlığı yok.");
    }
}
