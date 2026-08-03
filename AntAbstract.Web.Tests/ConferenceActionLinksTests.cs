using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// "Tüm Kongreler" listesindeki işlem ikonlarının gittiği adresleri doğrular:
/// kongre akışı, siteyi görüntüle, ayarlar (düzenle) ve kurum detayı.
///
/// Bağlantılar kongre slug'ı varsa onu, yoksa kurum slug'ını kullanıyor.
/// Admin route'ları kurum slug'ına göre çözüldüğü için bu ayrım önemli.
/// </summary>
public sealed class ConferenceActionLinksTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    // xUnit her [Fact] için sınıfı yeniden kurar; kimlikler sabit olmalı ki
    // tohumlama tekrar tekrar aynı kaydı eklemeye çalışmasın.
    private static readonly Guid _tenantId =
        new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid _conferenceId =
        new("22222222-2222-2222-2222-222222222222");

    private const string TenantSlug = "test-kurum";
    private const string ConferenceSlug = "test-kongre-2026";

    public ConferenceActionLinksTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Tenants.Any(t => t.Slug == TenantSlug))
        {
            db.Tenants.Add(new Tenant
            {
                Id = _tenantId,
                Slug = TenantSlug,
                Name = "Test Kurum"
            });

            db.Conferences.Add(new Conference
            {
                Id = _conferenceId,
                TenantId = _tenantId,
                Title = "Test Kongre 2026",
                Slug = ConferenceSlug,
                StartDate = DateTime.Today.AddDays(30),
                EndDate = DateTime.Today.AddDays(32)
            });

            db.SaveChanges();
        }
    }

    /// <summary>
    /// Kongre akışı ikonu kurum slug'ı ile açılmalı.
    /// </summary>
    [Fact]
    public async Task FlowIcon_WithTenantSlug_Opens()
    {
        var response = await _client.GetAsync(
            $"/{TenantSlug}/Admin/ConferenceFlow?conferenceId={_conferenceId}");

        _output.WriteLine($"{(int)response.StatusCode} akış (kurum slug) -> {response.Headers.Location}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Ayarlar (dişli) ikonu kurum slug'ı ile açılmalı.
    /// </summary>
    [Fact]
    public async Task SettingsIcon_WithTenantSlug_OpensEditPage()
    {
        var response = await _client.GetAsync(
            $"/{TenantSlug}/Admin/Conferences/Edit/{_conferenceId}");

        _output.WriteLine($"{(int)response.StatusCode} ayarlar (kurum slug) -> {response.Headers.Location}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Kural kaydı: admin route'ları kurum slug'ı bekliyor. Kongre slug'ı
    /// verilirse sayfa açılmaz, listeye geri yönlendirir. Liste görünümü bu
    /// yüzden admin bağlantılarında TenantSlug kullanmak zorunda.
    /// </summary>
    [Fact]
    public async Task AdminRoutes_RejectConferenceSlug_HenceLinksMustUseTenantSlug()
    {
        var response = await _client.GetAsync(
            $"/{ConferenceSlug}/Admin/Conferences/Edit/{_conferenceId}");

        _output.WriteLine($"{(int)response.StatusCode} ayarlar (kongre slug) -> {response.Headers.Location}");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Siteyi görüntüle ikonu kongrenin kendi slug'ını kullanır.</summary>
    [Fact]
    public async Task SiteIcon_OpensPublicConferencePage()
    {
        var response = await _client.GetAsync($"/{ConferenceSlug}");

        _output.WriteLine($"{(int)response.StatusCode} site");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Kurum detayı ikonu.</summary>
    [Fact]
    public async Task TenantIcon_OpensTenantDetails()
    {
        var response = await _client.GetAsync($"/Admin/Tenants/Details/{_tenantId}");

        _output.WriteLine($"{(int)response.StatusCode} kurum detayı");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
