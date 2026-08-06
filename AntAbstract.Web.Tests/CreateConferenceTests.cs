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
/// "Yeni Kongre Ekle" akışını uçtan uca çalıştırır: formu açar, doğrulama
/// jetonunu alır, gönderir ve kaydın gerçekten oluştuğunu doğrular.
/// </summary>
public sealed class CreateConferenceTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly Guid TenantId =
        new("55555555-5555-5555-5555-555555555555");

    public CreateConferenceTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Tenants.Any(t => t.Id == TenantId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Slug = "kongre-ekleme-kurum",
                Name = "Kongre Ekleme Test Kurumu"
            });

            db.SaveChanges();
        }
    }

    private async Task<string> GetTokenAsync()
    {
        var page = await _client.GetAsync("/Admin/AllConferences/Create");
        var html = await page.Content.ReadAsStringAsync();

        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.True(match.Success, "Formda doğrulama jetonu bulunamadı.");

        return match.Groups[1].Value;
    }

    [Fact]
    public async Task CreatePage_Opens()
    {
        var response = await _client.GetAsync("/Admin/AllConferences/Create");

        _output.WriteLine($"{(int)response.StatusCode} form sayfası");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidPost_CreatesConference()
    {
        var token = await GetTokenAsync();
        var slug = "test-kongre-" + Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsync(
            "/Admin/AllConferences/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["TenantId"] = TenantId.ToString(),
                ["Title"] = "Test Kongresi 2026",
                ["TitleEn"] = "Test Congress 2026",
                ["Slug"] = slug,
                ["StartDate"] = "2026-09-18",
                ["EndDate"] = "2026-09-20",
                ["City"] = "Bursa",
                ["Country"] = "Türkiye",
                ["Venue"] = "Kongre Merkezi"
            }));

        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} gönderim sonucu");

        // Form hatayla geri geldiyse nedenini görelim.
        if (response.StatusCode == HttpStatusCode.OK)
        {
            foreach (Match m in Regex.Matches(
                body, @"field-validation-error[^>]*>([^<]+)<"))
            {
                _output.WriteLine("  alan hatası: " + m.Groups[1].Value.Trim());
            }

            foreach (Match m in Regex.Matches(body, @"<li>(.*?)</li>"))
            {
                _output.WriteLine("  özet hatası: " + m.Groups[1].Value.Trim());
            }
        }

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var created = await db.Conferences
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Slug == slug);

        Assert.NotNull(created);
        Assert.Equal("Test Kongresi 2026", created!.Title);
        Assert.Equal(TenantId, created.TenantId);
        Assert.Equal("Bursa", created.City);
    }

    private async Task<HttpResponseMessage> PostAsync(
        string slug,
        string title = "Doğrulama Kongresi",
        string start = "2026-09-18",
        string end = "2026-09-20",
        string? tenantId = null)
    {
        var token = await GetTokenAsync();

        return await _client.PostAsync(
            "/Admin/AllConferences/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["TenantId"] = tenantId ?? TenantId.ToString(),
                ["Title"] = title,
                ["Slug"] = slug,
                ["StartDate"] = start,
                ["EndDate"] = end
            }));
    }

    /// <summary>
    /// Asıl soru: form üzerinden eklenen kongre "Tüm Kongreler" listesinde
    /// görünüyor mu? Liste global tenant filtresine tabi olduğu için kaydın
    /// veritabanında olması tek başına yeterli değil.
    /// </summary>
    [Fact]
    public async Task CreatedConference_AppearsInAllConferencesList()
    {
        var slug = "listede-gorunsun-" + Guid.NewGuid().ToString("N")[..8];

        var create = await PostAsync(slug, title: "Listede Görünen Kongre");
        Assert.Equal(HttpStatusCode.Found, create.StatusCode);

        var list = await _client.GetAsync("/Admin/AllConferences");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var html = await list.Content.ReadAsStringAsync();

        _output.WriteLine($"liste uzunluğu: {html.Length}, aranan slug: {slug}");

        Assert.Contains(slug, html);
    }

    /// <summary>
    /// Kongre Siteleri (CentralVitrin) ekranında da görünmeli.
    /// </summary>
    [Fact]
    public async Task CreatedConference_AppearsInCentralVitrinList()
    {
        var slug = "vitrinde-gorunsun-" + Guid.NewGuid().ToString("N")[..8];

        var create = await PostAsync(slug, title: "Vitrinde Görünen Kongre");
        Assert.Equal(HttpStatusCode.Found, create.StatusCode);

        var list = await _client.GetAsync("/Admin/CentralVitrin");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var html = await list.Content.ReadAsStringAsync();

        _output.WriteLine($"vitrin uzunluğu: {html.Length}, aranan slug: {slug}");

        Assert.Contains(slug, html);
    }

    /// <summary>
    /// "Yeni Site Oluştur" akışı: kongre eklendikten sonra site oluşturulunca
    /// varsayılan bölümler gerçekten yazılmalı.
    ///
    /// Buton önce Admin/Website/InitSite adresine gidiyordu; o controller
    /// TenantAdminOnly politikasında olduğu için SuperAdmin "Erişim Reddedildi"
    /// alıyordu.
    /// </summary>
    [Fact]
    public async Task InitSite_CreatesDefaultBlocks()
    {
        var slug = "site-olustur-" + Guid.NewGuid().ToString("N")[..8];

        var create = await PostAsync(slug, title: "Site Oluşturma Kongresi");
        Assert.Equal(HttpStatusCode.Found, create.StatusCode);

        Guid conferenceId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var conference = await db.Conferences
                .IgnoreQueryFilters()
                .FirstAsync(c => c.Slug == slug);

            conferenceId = conference.Id;

            Assert.False(
                await db.ConferencePageBlocks.AnyAsync(b => b.ConferenceId == conferenceId),
                "Yeni kongrenin başlangıçta bloğu olmamalı.");
        }

        // Site oluşturma formu açılıyor ve yeni kongreyi listeliyor mu?
        var form = await _client.GetAsync("/Admin/CentralVitrin/InitSite");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);

        var formHtml = await form.Content.ReadAsStringAsync();

        // SQLite GUID'leri büyük harfle üretir; karşılaştırma harf duyarsız.
        Assert.Contains(
            conferenceId.ToString(),
            formHtml,
            StringComparison.OrdinalIgnoreCase);

        var token = Regex.Match(
            formHtml,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var post = await _client.PostAsync(
            "/Admin/CentralVitrin/InitSite",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["conferenceId"] = conferenceId.ToString()
            }));

        _output.WriteLine($"{(int)post.StatusCode} site oluşturma");
        Assert.Equal(HttpStatusCode.Found, post.StatusCode);

        // Yönlendirilen sayfa blokları oluşturuyor.
        var target = post.Headers.Location!.ToString();
        var manage = await _client.GetAsync(target);
        Assert.Equal(HttpStatusCode.OK, manage.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var blocks = await db.ConferencePageBlocks
                .IgnoreQueryFilters()
                .Where(b => b.ConferenceId == conferenceId)
                .ToListAsync();

            _output.WriteLine($"oluşan bölüm sayısı: {blocks.Count}");

            Assert.NotEmpty(blocks);
            Assert.All(blocks, b => Assert.NotEqual(Guid.Empty, b.TenantId));
        }
    }

    /// <summary>
    /// Blok düzenleme kaydı: TempData'ya LocalizedString konulduğu için
    /// kaydetme 500 ile düşüyordu. Ayrıca yapılandırılmış içeriğin gerçekten
    /// yazıldığını doğrular.
    /// </summary>
    [Fact]
    public async Task EditBlock_SavesStructuredContent()
    {
        var slug = "blok-kaydet-" + Guid.NewGuid().ToString("N")[..8];

        Assert.Equal(HttpStatusCode.Found, (await PostAsync(slug, title: "Blok Kongresi")).StatusCode);

        Guid conferenceId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            conferenceId = (await db.Conferences.IgnoreQueryFilters()
                .FirstAsync(c => c.Slug == slug)).Id;
        }

        // ManageBlocks açılınca varsayılan bölümler oluşur.
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.GetAsync($"/Admin/CentralVitrin/ManageBlocks?conferenceId={conferenceId}")).StatusCode);

        int topicsBlockId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            topicsBlockId = (await db.ConferencePageBlocks.IgnoreQueryFilters()
                .FirstAsync(b => b.ConferenceId == conferenceId &&
                                 b.BlockType == ConferencePageBlockType.Topics)).Id;
        }

        var editPage = await _client.GetAsync($"/Admin/CentralVitrin/EditBlock/{topicsBlockId}");
        Assert.Equal(HttpStatusCode.OK, editPage.StatusCode);

        var token = Regex.Match(
            await editPage.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var save = await _client.PostAsync(
            $"/Admin/CentralVitrin/EditBlock/{topicsBlockId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = topicsBlockId.ToString(),
                ["ConferenceId"] = conferenceId.ToString(),
                ["Title"] = "Kongre Konuları",
                ["IsActive"] = "true",
                ["repeaterReady"] = "1",
                ["topicsContent.Description"] = "Kabul edilen konular",
                ["topicsContent.Items[0].Name"] = "Yapay Zekâ",
                ["topicsContent.Items[0].Description"] = "Makine öğrenmesi",
                ["topicsContent.Items[1].Name"] = "Biyoteknoloji",
                ["topicsContent.Items[1].Description"] = ""
            }));

        _output.WriteLine($"{(int)save.StatusCode} blok kaydetme");
        Assert.Equal(HttpStatusCode.Found, save.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var json = (await db.ConferencePageBlocks.IgnoreQueryFilters()
                .FirstAsync(b => b.Id == topicsBlockId)).ContentJson;

            _output.WriteLine("kaydedilen içerik: " + json);

            var content = System.Text.Json.JsonSerializer
                .Deserialize<AntAbstract.Web.Models.WebsiteBlocks.TopicsBlockContent>(
                    json!,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            Assert.NotNull(content);
            Assert.Equal("Kabul edilen konular", content!.Description);
            Assert.Equal(2, content.Items.Count);
            Assert.Equal("Yapay Zekâ", content.Items[0].Name);
            Assert.Equal("Biyoteknoloji", content.Items[1].Name);
        }
    }

    private async Task<bool> ExistsAsync(string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Conferences.IgnoreQueryFilters().AnyAsync(c => c.Slug == slug);
    }

    /// <summary>
    /// Slug benzersiz indeksli; kontrol edilmezse kayıt 500 ile düşerdi.
    /// </summary>
    [Fact]
    public async Task DuplicateSlug_IsRejected_WithoutServerError()
    {
        var slug = "cift-slug-" + Guid.NewGuid().ToString("N")[..8];

        var first = await PostAsync(slug);
        Assert.Equal(HttpStatusCode.Found, first.StatusCode);

        var second = await PostAsync(slug, title: "İkinci Kongre");

        _output.WriteLine($"{(int)second.StatusCode} ikinci gönderim");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.Conferences
            .IgnoreQueryFilters()
            .CountAsync(c => c.Slug == slug);

        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("Büyük Harfli Slug")]
    [InlineData("bosluk iceren")]
    [InlineData("-bastan-tire")]
    public async Task InvalidSlugFormat_IsRejected(string slug)
    {
        var response = await PostAsync(slug);

        _output.WriteLine($"{(int)response.StatusCode} slug='{slug}'");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await ExistsAsync(slug), "Geçersiz slug kaydedilmemeliydi.");
    }

    [Fact]
    public async Task EndDateBeforeStartDate_IsRejected()
    {
        var slug = "ters-tarih-" + Guid.NewGuid().ToString("N")[..8];

        var response = await PostAsync(slug, start: "2026-09-20", end: "2026-09-18");

        _output.WriteLine($"{(int)response.StatusCode} ters tarih");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await ExistsAsync(slug), "Ters tarihli kongre kaydedilmemeliydi.");
    }

    [Fact]
    public async Task UnknownTenant_IsRejected()
    {
        var slug = "olmayan-kurum-" + Guid.NewGuid().ToString("N")[..8];

        var response = await PostAsync(
            slug, tenantId: "99999999-9999-9999-9999-999999999999");

        _output.WriteLine($"{(int)response.StatusCode} bilinmeyen kurum");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await ExistsAsync(slug), "Olmayan kuruma kongre eklenmemeliydi.");
    }

    [Fact]
    public async Task PostWithoutTenant_DoesNotCreate()
    {
        var token = await GetTokenAsync();
        var slug = "kurumsuz-" + Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsync(
            "/Admin/AllConferences/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["TenantId"] = Guid.Empty.ToString(),
                ["Title"] = "Kurumsuz Kongre",
                ["Slug"] = slug,
                ["StartDate"] = "2026-09-18",
                ["EndDate"] = "2026-09-20"
            }));

        _output.WriteLine($"{(int)response.StatusCode} kurumsuz gönderim");

        // Formu hatayla geri göstermeli, yönlendirmemeli.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(
            await db.Conferences.IgnoreQueryFilters().AnyAsync(c => c.Slug == slug),
            "Kurum seçilmeden kongre oluşturulmamalıydı.");
    }
}
