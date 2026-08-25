using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace AntAbstract.Web.Tests.Smoke;

/// <summary>
/// Kayıt kapalı / tarihi geçmiş / kontenjanı dolmuş kongrelerde
/// ön kayıt ekranının gösterilmediğini doğrular.
/// </summary>
public sealed class RegistrationAvailabilityTests
{
    private const string ClosedMarker = "class=\"closed-card\"";
    private const string TicketMarker = "class=\"ticket-card";

    private static HttpClient CreateClient(RegistrationAvailabilityFactory factory)
    {
        return factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task OpenConference_ShowsRegistrationTypes()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("acik-kongre");

        var html = await CreateClient(factory).GetStringAsync("/acik-kongre/registration");

        Assert.Contains(TicketMarker, html);
        Assert.DoesNotContain(ClosedMarker, html);
    }

    [Fact]
    public async Task ClosedRegistration_HidesRegistrationTypes()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("kapali-kongre", isRegistrationOpen: false);

        var html = await CreateClient(factory).GetStringAsync("/kapali-kongre/registration");

        Assert.Contains(ClosedMarker, html);
        Assert.DoesNotContain(TicketMarker, html);
    }

    [Fact]
    public async Task PastConference_HidesRegistrationTypes()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference(
            "gecmis-kongre",
            endDate: DateTime.UtcNow.Date.AddDays(-1));

        var html = await CreateClient(factory).GetStringAsync("/gecmis-kongre/registration");

        Assert.Contains(ClosedMarker, html);
        Assert.DoesNotContain(TicketMarker, html);
    }

    [Fact]
    public async Task FullQuota_HidesRegistrationTypes()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference(
            "dolu-kongre",
            maxRegistrations: 2,
            existingRegistrationCount: 2);

        var html = await CreateClient(factory).GetStringAsync("/dolu-kongre/registration");

        Assert.Contains(ClosedMarker, html);
        Assert.DoesNotContain(TicketMarker, html);
    }

    [Fact]
    public async Task ExpiredRegistrationType_ShowsEmptyStateInsteadOfTicket()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference(
            "suresi-dolmus-tur",
            registrationTypeDeadline: DateTime.UtcNow.Date.AddDays(-1));

        var html = await CreateClient(factory).GetStringAsync("/suresi-dolmus-tur/registration");

        Assert.DoesNotContain(TicketMarker, html);
        Assert.Contains("class=\"empty-card\"", html);
    }

    [Fact]
    public async Task ClosedRegistration_CheckoutRedirectsBack()
    {
        using var factory = new RegistrationAvailabilityFactory();
        var (_, conference) = factory.SeedConference("kapali-checkout", isRegistrationOpen: false);

        Guid typeId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AntAbstract.Infrastructure.Context.AppDbContext>();

            var tenantContext = scope.ServiceProvider
                .GetRequiredService<AntAbstract.Infrastructure.Context.TenantContext>();

            tenantContext.IsGlobalContext = true;

            typeId = db.RegistrationTypes
                .First(rt => rt.ConferenceId == conference.Id).Id;
        }

        var response = await CreateClient(factory)
            .GetAsync($"/kapali-checkout/registration/checkout/{typeId}");

        // Anonim kullanıcı önce giriş ekranına, giriş yapmışsa kayıt ekranına döner.
        // Her hâlükârda ödeme ekranı gösterilmemelidir.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.ToString() ?? "";
        var locationPath = location.Split('?')[0];

        Assert.DoesNotContain("checkout", locationPath);
    }

    [Fact]
    public async Task ClosedRegistration_CongressListHidesRegisterButton()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("liste-kapali", isRegistrationOpen: false);

        var html = await CreateClient(factory).GetStringAsync("/congresses");

        Assert.Contains("congress-action-button btn-closed-congress", html);
        Assert.DoesNotContain("congress-action-button btn-register-congress", html);
    }

    [Fact]
    public async Task MissingConference_RedirectShowsErrorMessage()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("var-olan-kongre");

        var client = factory.CreateClient();

        // Yönlendirme takip edilir: kullanıcı bir açıklama görmelidir.
        var response = await client.GetAsync("/olmayan-slug/registration");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("notfound-card", html);
    }

    [Fact]
    public async Task UnknownSlug_Returns404NotLandingPage()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("gercek-kongre");

        var response = await CreateClient(factory).GetAsync("/sacma-bir-slug");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("notfound-card", html);
        Assert.DoesNotContain("hero-modern", html);
    }

    [Fact]
    public async Task ExistingSlug_StillRendersConferenceSite()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("gercek-kongre");

        var response = await CreateClient(factory).GetAsync("/gercek-kongre");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("conference-hero", html);
    }

    [Fact]
    public async Task HomePage_IsNotAffectedBy404Rule()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("gercek-kongre");

        var response = await CreateClient(factory).GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hero-modern", html);
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/forgot-password")]
    [InlineData("/access-denied")]
    public async Task SingleSegmentRazorPages_StillReachable(string url)
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("gercek-kongre");

        var response = await CreateClient(factory).GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Payment")]
    [InlineData("/Home/Congresses")]
    [InlineData("/congresses")]
    public async Task KnownControllerSegments_AreNotTreatedAsMissingCongress(string url)
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("gercek-kongre");

        var response = await CreateClient(factory).GetAsync(url);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenConference_CongressListShowsRegisterButton()
    {
        using var factory = new RegistrationAvailabilityFactory();
        factory.SeedConference("liste-acik");

        var html = await CreateClient(factory).GetStringAsync("/congresses");

        Assert.Contains("congress-action-button btn-register-congress", html);
        Assert.DoesNotContain("congress-action-button btn-closed-congress", html);
    }
}
