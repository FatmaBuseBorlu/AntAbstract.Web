using System.Net;

namespace AntAbstract.Web.Tests.Smoke;

/// <summary>
/// Smoke testleri: uygulamanın kritik route'larının 500 dönmediğini ve
/// kimlik doğrulama gerektiren sayfaların yetkisiz kullanıcıları login'e
/// yönlendirdiğini doğrular.
/// </summary>
[Collection("Smoke")]
public sealed class SmokeTests(SmokeTestFactory factory) : IClassFixture<SmokeTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new() { AllowAutoRedirect = false });

    // ── Genel erişime açık sayfalar ─────────────────────────────────────────

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/Index")]
    public async Task PublicPages_DoNotReturn500(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // ── Sağlık kontrolü ──────────────────────────────────────────────────────

    [Fact]
    public async Task HealthEndpoint_IsAnonymousAndHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    // ── SEO ve hata sayfası ──────────────────────────────────────────────────

    [Fact]
    public async Task RobotsTxt_PointsToSitemapAndHidesPanels()
    {
        var body = await _client.GetStringAsync("/robots.txt");

        Assert.Contains("Sitemap: ", body);
        Assert.Contains("Disallow: /Admin/", body);
    }

    [Fact]
    public async Task SitemapXml_ListsPublicPages()
    {
        var response = await _client.GetAsync("/sitemap.xml");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<urlset", body);
        Assert.Contains("/congresses</loc>", body);
    }

    [Fact]
    public async Task UnknownPage_ShowsDesigned404ForBrowsers()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/olmayan/bir/sayfa/adresi/burada");
        request.Headers.Accept.ParseAdd("text/html");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("notfound-card", body);
    }

    [Fact]
    public async Task UnknownApiPath_Stays404WithoutHtml()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/olmayan/adres/burada/x");
        request.Headers.Accept.ParseAdd("text/html");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("notfound-card", body);
    }

    // ── Identity Razor Pages ─────────────────────────────────────────────────

    [Fact]
    public async Task LoginPage_Returns200()
    {
        var response = await _client.GetAsync("/Identity/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterPage_Returns200()
    {
        var response = await _client.GetAsync("/Identity/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Admin area — kimlik doğrulama gerektirir ─────────────────────────────

    [Theory]
    [InlineData("/Admin/Dashboard")]
    [InlineData("/Admin/Conferences")]
    [InlineData("/Admin/AuditLogs")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Admin/EmailTemplates")]
    [InlineData("/Admin/Payments")]
    [InlineData("/Admin/Health")]
    public async Task AdminRoutes_RedirectToLoginWhenUnauthenticated(string url)
    {
        var response = await _client.GetAsync(url);

        // Kimlik doğrulaması gerektiren sayfalar login'e yönlendirmeli (302)
        // veya 401 dönmeli — 500 ya da 200 değil.
        Assert.True(
            response.StatusCode is HttpStatusCode.Found
                              or HttpStatusCode.Unauthorized,
            $"{url} beklenen yönlendirme/401 yerine {(int)response.StatusCode} döndü.");
    }

    // ── Güvenli indirme — kimlik doğrulama gerektirir ────────────────────────

    [Fact]
    public async Task SecureDownload_RedirectsToLoginWhenUnauthenticated()
    {
        var response = await _client.GetAsync("/download/submission/00000000-0000-0000-0000-000000000001");

        Assert.True(
            response.StatusCode is HttpStatusCode.Found
                              or HttpStatusCode.Unauthorized
                              or HttpStatusCode.NotFound,
            $"Beklenen 302/401/404 yerine {(int)response.StatusCode} döndü.");
    }

    // ── Health/Status JSON endpoint ──────────────────────────────────────────

    [Fact]
    public async Task HealthStatus_Returns401WhenNoApiKey()
    {
        var response = await _client.GetAsync("/Admin/Health/Status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthStatus_Returns401WhenApiKeyNotConfigured()
    {
        // appsettings'te SET_VIA_ENV_OR_SECRETS placeholder'ı var — yapılandırılmamış sayılır
        var request = new HttpRequestMessage(HttpMethod.Get, "/Admin/Health/Status");
        request.Headers.Add("X-Health-Key", "herhangi-bir-deger");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 404 beklenen rotalar — asla 500 dönmemeli ────────────────────────────

    [Theory]
    [InlineData("/bu-rota-kesinlikle-yok")]
    [InlineData("/Admin/OlmayanController")]
    public async Task UnknownRoutes_Return404NotCrash(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
