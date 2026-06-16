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
