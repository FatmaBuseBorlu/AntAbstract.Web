using System.Net;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Sertifikalar, Kayıtlar, Raporlar ve Website Yönetimi ekranları
/// TenantAdminOnly politikasındaydı; SuperAdmin hiçbir kuruma bağlı olmadığı
/// için (DbInitializer TenantId = null yapıyor) dördü de "Erişim Reddedildi"
/// veriyordu ve bu yüzden menüden gizlenmişlerdi. Bu testler ekranların
/// SuperAdmin'e gerçekten açıldığını doğruluyor.
/// </summary>
public sealed class SuperAdminManagementScreensTests
    : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "yonetim-ekrani-kurum";

    private static readonly Guid TenantId =
        new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static readonly Guid ConferenceId =
        new("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public SuperAdminManagementScreensTests(
        AuthenticatedTestFactory factory,
        ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = Slug,
            Name = "Yönetim Ekranı Kurumu"
        });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Yönetim Ekranı Kongresi",
            Slug = "yonetim-ekrani-kongresi",
            StartDate = DateTime.Today.AddDays(90),
            EndDate = DateTime.Today.AddDays(92)
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Kayıtlar ve Raporlar kongre seçme ekranıyla başlıyor. Eskiden politika
    /// SuperAdmin'i daha kapıda çevirdiği için buraya hiç gelinemiyordu.
    /// </summary>
    [Theory]
    [InlineData("/Admin/Registrations")]
    [InlineData("/Admin/Reports")]
    public async Task SelectConferencePages_Open(string url)
    {
        var response = await _client.GetAsync(url);

        _output.WriteLine($"{(int)response.StatusCode} {url}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            ConferenceId.ToString(),
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sertifika yönetimi listesi doğrudan açılıyor; kapsam kurum kimliğinden
    /// değil erişilebilir kongre sorgusundan geldiği için SuperAdmin'de artık
    /// boş liste yerine gerçek veri dönüyor.
    /// </summary>
    [Fact]
    public async Task CertificatesAdminPage_Opens()
    {
        var response = await _client.GetAsync($"/{Slug}/Admin/Certificates");

        _output.WriteLine($"{(int)response.StatusCode} sertifika yönetimi");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Website Yönetimi kurum adresinden açılıyor. Kurum eşleşmesi şartı
    /// SuperAdmin'i dışarıda bırakıyordu; artık kongre listesi de doluyor.
    /// Kongre adı değil kimliği aranıyor: Razor Türkçe karakterleri sayısal
    /// HTML varlığına çeviriyor.
    /// </summary>
    [Fact]
    public async Task WebsiteAdminPage_OpensAndListsConference()
    {
        var response = await _client.GetAsync($"/{Slug}/Admin/Website");

        _output.WriteLine($"{(int)response.StatusCode} website yönetimi");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            ConferenceId.ToString(),
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Menü, dört bağlantının hiçbirini SuperAdmin'e göstermiyordu; ekranlar
    /// yalnızca adres elle yazılarak bulunabiliyordu. Menü bloğu ancak kongre
    /// seçiliyken açıldığı için önce seçim yapılıyor.
    /// </summary>
    [Fact]
    public async Task Menu_ShowsAllFourManagementLinks()
    {
        await SelectConferenceAsync();

        var response = await _client.GetAsync(
            $"/{Slug}/Admin/Registrations?conferenceId={ConferenceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        var links = new Dictionary<string, string>
        {
            ["Sertifikalar"] = $"/{Slug}/Certificates",
            ["Kayıtlar ve Ödemeler"] = $"/{Slug}/Admin/Registrations",
            ["Raporlar"] = $"/{Slug}/Admin/Reports",
            ["Website Yönetimi"] = $"/{Slug}/Admin/Website"
        };

        foreach (var (label, href) in links)
        {
            var found = html.Contains(href, StringComparison.OrdinalIgnoreCase);

            _output.WriteLine($"menüde {label}: {found}");

            Assert.True(found, $"{label} menüde görünmüyor.");
        }
    }

    /// <summary>Kongreyi oturuma yazar; menü bloğu buna bağlı.</summary>
    private async Task SelectConferenceAsync()
    {
        var page = await _client.GetAsync("/Admin/Registrations");

        var token = Regex.Match(
            await page.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var selected = await _client.PostAsync(
            "/Admin/Registrations/Select",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["conferenceId"] = ConferenceId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, selected.StatusCode);
    }
}
