using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Menüdeki bağlantılar gerçekten bir yere gitmeli.
///
/// Konuşmacılar ve Sponsorlar denetleyicilerinde yalnızca slug'lı rota
/// tanımlıydı; menü ise kongre seçilmemişken slug'sız adrese bağlanıyordu
/// ve bağlantı 404 veriyordu. Kullanıcı ekranın bozuk olduğunu sanıyordu.
///
/// Test bağlantının adresini menüden değil, doğrudan deneyerek kontrol
/// ediyor: 404 dönmemeli, ya ekran açılmalı ya da kongre seçimine
/// yönlendirmeli.
/// </summary>
public sealed class MenuLinkReachabilityTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    private static readonly Guid TenantId = new("cafe0000-1111-2222-3333-444455556666");
    private static readonly Guid ConferenceId = new("cafe0000-1111-2222-3333-444455556667");
    private const string TenantSlug = "menu-link-kurum";

    public MenuLinkReachabilityTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Menü Bağlantı Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Menü Bağlantı Kongresi 2026",
            Slug = "menu-link-kongre",
            StartDate = DateTime.Today.AddDays(20),
            EndDate = DateTime.Today.AddDays(22),
            City = "Ankara",
            Country = "Türkiye"
        });

        db.SaveChanges();
    }

    private HttpClient Admin()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "SuperAdmin");
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "menu-link-test");
        return client;
    }

    [Theory]
    [InlineData("/Admin/Speakers")]
    [InlineData("/Admin/Sponsors")]
    [InlineData("/Admin/RegistrationTypes")]
    [InlineData("/Admin/ConferenceTopics")]
    public async Task SlugsuzMenuAdresi_404Donmuyor(string path)
    {
        var response = await Admin().GetAsync(path);
        var code = (int)response.StatusCode;

        _output.WriteLine($"{code} {path} -> {response.Headers.Location}");

        Assert.False(
            code == 404,
            $"{path} 404 döndü; menüdeki bağlantı hiçbir yere gitmiyor.");

        Assert.True(code < 500, $"{path} sunucu hatası verdi: {code}");
    }

    /// <summary>
    /// Kongre seçilmemişken kullanıcı boşluğa düşmemeli; seçim ekranına
    /// gitmeli ve oradan geri dönebilmeli.
    /// </summary>
    [Theory]
    [InlineData("/Admin/Speakers")]
    [InlineData("/Admin/Sponsors")]
    public async Task KongreSecilmemisse_SecimEkraninaYonlendiriyor(string path)
    {
        var response = await Admin().GetAsync(path);
        var target = response.Headers.Location?.ToString() ?? "";

        _output.WriteLine($"{(int)response.StatusCode} {path} -> {target}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("SelectConference", target, StringComparison.OrdinalIgnoreCase);

        // Seçimden sonra kullanıcıyı geri getirecek adres taşınmalı.
        Assert.Contains(Uri.EscapeDataString(path), target, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kongre belliyken ekranın kendisine gitmeli — seçim ekranına değil.
    /// </summary>
    [Theory]
    [InlineData("/Admin/Speakers")]
    [InlineData("/Admin/Sponsors")]
    public async Task KongreBelliyse_EkranaGoturuyor(string path)
    {
        var response = await Admin().GetAsync($"{path}?conferenceId={ConferenceId}");
        var target = response.Headers.Location?.ToString() ?? "";

        _output.WriteLine($"{(int)response.StatusCode} {path} -> {target}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("SelectConference", target, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith($"/{TenantSlug}{path}", target, StringComparison.OrdinalIgnoreCase);
    }
}
