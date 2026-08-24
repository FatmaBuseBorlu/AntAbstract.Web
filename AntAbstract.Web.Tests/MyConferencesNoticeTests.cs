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
/// Kongre seçim ekranı aynı anda iki mesaj gösteriyordu: sayfanın tepesinde
/// kırmızı uyarı şeridi ("Kongre bulunamadı.") ve hemen altında aynı şeyi
/// söyleyen karşılama kartı ("...aşağıdan seçiniz"). Üstelik kırmızı şerit,
/// kongresi hemen altında listelenen kullanıcıya "hiç kongren yok" gibi
/// okunuyordu. Mesaj artık kartın içinde; ekranda tek yönlendirme var.
/// </summary>
public sealed class MyConferencesNoticeTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string AuthorId = "uyari-yazari";

    private static readonly Guid TenantId =
        new("1a1a1a1a-1a1a-1a1a-1a1a-1a1a1a1a1a1a");

    private static readonly Guid ConferenceId =
        new("1b1b1b1b-1b1b-1b1b-1b1b-1b1b1b1b1b1b");

    public MyConferencesNoticeTests(
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
            FirstName = "Uyarı",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = "uyari-kurum",
            Name = "Uyarı Kurumu"
        });

        var typeId = Guid.NewGuid();

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Uyarı Kongresi",
            Slug = "uyari-kongresi",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32)
        });

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = typeId,
            ConferenceId = ConferenceId,
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
            ConferenceId = ConferenceId,
            RegistrationTypeId = typeId,
            RegistrationDate = DateTime.UtcNow,
            IsPaid = false,
            Amount = 0
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Var olmayan kongre seçimi kullanıcıyı bu ekrana geri atıyor; ekrandaki
    /// durumun birebir aynısı.
    /// </summary>
    [Fact]
    public async Task BounceMessage_ShowsOnceInsideWelcomeCard()
    {
        var page = await _client.GetAsync("/Dashboard/MyConferences");

        var token = Regex.Match(
            await page.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var post = await _client.PostAsync(
            "/Dashboard/SelectConferencePost",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["conferenceId"] = Guid.NewGuid().ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        var bounced = await _client.GetAsync(
            post.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.OK, bounced.StatusCode);

        var html = await bounced.Content.ReadAsStringAsync();

        var separateBar = Regex.IsMatch(
            html, "class=\"alert alert-danger[^\"]*selection-alert");

        var cardNotice = Regex.IsMatch(
            html, "class=\"selection-notice selection-notice-");

        _output.WriteLine($"ayrı şerit: {separateBar}, kart içi: {cardNotice}");

        Assert.False(separateBar, "Sayfanın tepesinde ayrı uyarı şeridi hâlâ var.");
        Assert.True(cardNotice, "Mesaj karşılama kartında görünmüyor.");
    }

    /// <summary>Mesaj yokken kart normal hâlinde kalmalı.</summary>
    [Fact]
    public async Task WithoutMessage_CardStaysPlain()
    {
        var response = await _client.GetAsync("/Dashboard/MyConferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.False(
            Regex.IsMatch(html, "class=\"selection-notice selection-notice-"),
            "Mesaj yokken kartta uyarı rozeti çıkıyor.");
    }
}
