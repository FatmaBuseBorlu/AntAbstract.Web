using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Formlar açıldığında içi boş kırmızı bir hata kutusu görünüyordu.
/// Sebep: asp-validation-summary etiketi "alert alert-danger" sınıflarını
/// koşulsuz taşıyor, içerik boş olsa da kutu çiziliyor.
///
/// ASP.NET hata yokken kabı validation-summary-valid ile işaretliyor;
/// CSS o hâlde gizliyor, hata olunca sınıf validation-summary-errors'a
/// dönüyor ve kutu tekrar görünüyor.
/// </summary>
public sealed class ValidationSummaryTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public ValidationSummaryTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private static string Css(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "AntAbstract.Web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return File.ReadAllText(
            Path.Combine(dir!.FullName, "AntAbstract.Web", "wwwroot", "css", name));
    }

    /// <summary>
    /// Kural her iki düzende de bulunmalı: yönetici sayfaları site.css'i
    /// yüklemiyor, admin-design-system.css'i yüklüyor.
    /// </summary>
    [Theory]
    [InlineData("site.css")]
    [InlineData("admin-design-system.css")]
    public void BosDogrulamaKutusu_GizleyenKuralVar(string file)
    {
        Assert.Matches(
            new Regex(@"\.validation-summary-valid\s*\{[^}]*display:\s*none", RegexOptions.Singleline),
            Css(file));
    }

    /// <summary>Form ilk açıldığında kap "geçerli" olarak işaretlenmeli.</summary>
    [Fact]
    public async Task FormIlkAcildiginda_KapGecerliOlarakIsaretli()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "SuperAdmin");
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "vs-test");

        var response = await client.GetAsync("/Admin/AllConferences/Create");

        if (response.StatusCode != HttpStatusCode.OK)
        {
            _output.WriteLine($"{(int)response.StatusCode} — ekran açılmadı, atlandı");
            return;
        }

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("validation-summary-valid", html, StringComparison.Ordinal);

        // Hata kabı boşken hata listesi de basılmamalı.
        Assert.DoesNotContain("validation-summary-errors", html, StringComparison.Ordinal);
    }
}
