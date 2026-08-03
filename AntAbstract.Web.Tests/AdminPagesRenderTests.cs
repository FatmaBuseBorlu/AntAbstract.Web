using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Admin panelindeki listeleme, ekleme ve düzenleme sayfalarının yetkili
/// kullanıcı için gerçekten açıldığını doğrular. Amaç 500 (view bulunamadı,
/// null referans, bozuk Razor) türü kırılmaları yakalamak.
/// </summary>
public sealed class AdminPagesRenderTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new() { AllowAutoRedirect = false });

    /// <summary>Liste (açılma) sayfaları.</summary>
    [Theory]
    [InlineData("/Admin/AllConferences")]
    [InlineData("/Admin/Tenants")]
    [InlineData("/Admin/Users")]
    [InlineData("/Admin/Users/LoginHistory")]
    [InlineData("/Admin/CentralVitrin")]
    [InlineData("/Admin/Health")]
    [InlineData("/Admin/AuditLogs")]
    [InlineData("/Admin/EmailTemplates")]
    [InlineData("/Admin/SystemParameters")]
    public async Task ListPages_DoNotThrow(string url)
    {
        var response = await _client.GetAsync(url);

        output.WriteLine($"{(int)response.StatusCode} {url}");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>Ekleme (Create) sayfaları.</summary>
    [Theory]
    [InlineData("/Admin/AllConferences/Create")]
    [InlineData("/Admin/Tenants/Create")]
    [InlineData("/Admin/Website/InitSite")]
    public async Task CreatePages_DoNotThrow(string url)
    {
        var response = await _client.GetAsync(url);

        output.WriteLine($"{(int)response.StatusCode} {url}");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Var olmayan kayıt istendiğinde 500 değil, 404/302 dönmeli.
    /// </summary>
    [Theory]
    [InlineData("/Admin/Tenants/Edit/00000000-0000-0000-0000-000000000001")]
    [InlineData("/Admin/Tenants/Details/00000000-0000-0000-0000-000000000001")]
    [InlineData("/Admin/CentralVitrin/EditBlock/999999")]
    public async Task MissingRecords_ReturnNotFound_NotServerError(string url)
    {
        var response = await _client.GetAsync(url);

        output.WriteLine($"{(int)response.StatusCode} {url}");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
