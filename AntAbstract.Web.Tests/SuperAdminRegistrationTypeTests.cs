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
/// Kayıt türü tanımlı olmayan bir kongreye kimse kaydolamıyor, dolayısıyla
/// özet de gönderilemiyor. Bu ekran TenantAdminOnly politikasındaydı ve
/// SuperAdmin'i dışlıyordu; SuperAdmin hiçbir kuruma bağlı olmadığı için
/// (DbInitializer TenantId = null yapıyor) kayıt türü hiç eklenemiyordu.
/// </summary>
public sealed class SuperAdminRegistrationTypeTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "kayit-turu-kurum";

    private static readonly Guid TenantId =
        new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid ConferenceId =
        new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public SuperAdminRegistrationTypeTests(
        AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Kayıt Türü Kurumu" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Kayıt Türü Kongresi",
            Slug = "kayit-turu-kongresi",
            StartDate = DateTime.Today.AddDays(60),
            EndDate = DateTime.Today.AddDays(62)
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Kongre seçme ekranı açılmalı — önce "Admin hesabınıza bağlı kurum
    /// bulunamadı" diyip panele geri atıyordu.
    /// </summary>
    [Fact]
    public async Task SelectConferencePage_Opens()
    {
        var response = await _client.GetAsync("/Admin/RegistrationTypes");

        _output.WriteLine($"{(int)response.StatusCode} kongre seçme ekranı");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(ConferenceId.ToString(), html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kongre seçildikten sonra sol menüde Kayıt Türleri ve Bildiri Konuları
    /// görünmeli. Menü bloğu eskiden !isSuperAdmin koşulundaydı, yani bu
    /// bağlantılar Sistem Yöneticisine hiç gösterilmiyordu.
    /// </summary>
    [Fact]
    public async Task AfterSelectingConference_MenuShowsConfigLinks()
    {
        var page = await _client.GetAsync("/Admin/RegistrationTypes");
        var token = Regex.Match(
            await page.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var selected = await _client.PostAsync(
            "/Admin/RegistrationTypes/Select",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["conferenceId"] = ConferenceId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, selected.StatusCode);

        var listing = await _client.GetAsync($"/{Slug}/Admin/RegistrationTypes");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);

        var html = await listing.Content.ReadAsStringAsync();

        var hasTypes = html.Contains("/Admin/RegistrationTypes");
        var hasTopics = html.Contains("/Admin/ConferenceTopics");

        _output.WriteLine($"menüde Kayıt Türleri: {hasTypes}, Bildiri Konuları: {hasTopics}");

        Assert.True(hasTypes, "Kayıt Türleri menüde görünmüyor.");
        Assert.True(hasTopics, "Bildiri Konuları menüde görünmüyor.");
    }

    /// <summary>Asıl akış: kayıt türü gerçekten oluşturulabilmeli.</summary>
    [Fact]
    public async Task RegistrationType_CanBeCreated()
    {
        // Kongreyi seç (oturuma yazılır)
        var selectPage = await _client.GetAsync("/Admin/RegistrationTypes");
        var selectToken = Regex.Match(
            await selectPage.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var selected = await _client.PostAsync(
            "/Admin/RegistrationTypes/Select",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = selectToken,
                ["conferenceId"] = ConferenceId.ToString()
            }));

        _output.WriteLine($"{(int)selected.StatusCode} kongre seçimi");
        Assert.Equal(HttpStatusCode.Redirect, selected.StatusCode);

        // Ekleme formu açılmalı
        var form = await _client.GetAsync($"/{Slug}/Admin/RegistrationTypes/Create");

        _output.WriteLine($"{(int)form.StatusCode} ekleme formu");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);

        var token = Regex.Match(
            await form.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var save = await _client.PostAsync(
            $"/{Slug}/Admin/RegistrationTypes/Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ConferenceId"] = ConferenceId.ToString(),
                ["Slug"] = Slug,
                ["Name"] = "Akademisyen",
                ["Description"] = "Bildiri gönderebilir",
                ["Price"] = "2500",
                ["Currency"] = "TRY",
                ["RoleName"] = "Author"
            }));

        _output.WriteLine($"{(int)save.StatusCode} kayıt türü kaydetme");
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var created = await db.RegistrationTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ConferenceId == ConferenceId && r.Name == "Akademisyen");

        Assert.True(created != null, "Kayıt türü oluşturulmadı.");
        Assert.Equal("Author", created!.RoleName);
        Assert.True(created.IsActive, "Yeni kayıt türü aktif olmalı.");
        Assert.Null(created.Deadline);
    }
}
