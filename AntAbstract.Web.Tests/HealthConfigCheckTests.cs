using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Ödeme ve e-posta anahtarları yalnızca sunucudaki appsettings.Production.json
/// içinde duruyor; depodaki dosya <c>#{...}#</c> yer tutucusu tutuyor ve deploy
/// paketine hiç girmiyor. Bu yüzden "canlıda anahtarlar gerçekten dolu mu"
/// sorusunun tek pratik cevabı Sistem Durumu ekranı.
///
/// Buradaki testler o ekranın yer tutucuyu gerçek değerden ayırt ettiğini
/// doğruluyor — ayırt edemezse ekran yanlış yere yeşil yakar ve asıl işini
/// yapmaz.
/// </summary>
public sealed class HealthConfigCheckTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public HealthConfigCheckTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private HttpClient CreateClient(Dictionary<string, string?> settings) =>
        _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration(
                (_, cfg) => cfg.AddInMemoryCollection(settings)))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static readonly Dictionary<string, string?> Placeholders = new()
    {
        ["Stripe:SecretKey"] = "#{STRIPE_SECRET_KEY}#",
        ["PayTR:MerchantId"] = "#{PAYTR_MERCHANT_ID}#",
        ["PayTR:MerchantKey"] = "#{PAYTR_MERCHANT_KEY}#",
        ["PayTR:MerchantSalt"] = "#{PAYTR_MERCHANT_SALT}#",
        ["Email:SmtpServer"] = "#{SMTP_SERVER}#",
        ["Email:Username"] = "#{SMTP_USERNAME}#",
        ["Email:Password"] = "#{SMTP_PASSWORD}#"
    };

    private static readonly Dictionary<string, string?> RealValues = new()
    {
        ["Stripe:SecretKey"] = "sk_live_ornek",
        ["PayTR:MerchantId"] = "123456",
        ["PayTR:MerchantKey"] = "anahtar",
        ["PayTR:MerchantSalt"] = "tuz",
        ["PayTR:TestMode"] = "false",
        ["Email:SmtpServer"] = "smtp.ornek.com",
        ["Email:Username"] = "no-reply@ornek.com",
        ["Email:Password"] = "parola"
    };

    private async Task<JsonElement> StatusAsync(Dictionary<string, string?> settings)
    {
        var response = await CreateClient(settings).GetAsync("/Admin/Health/Status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("checks");
    }

    [Fact]
    public async Task YerTutucuVarken_UcuDeYapilandirilmamisGorunur()
    {
        var checks = await StatusAsync(Placeholders);

        _output.WriteLine(checks.ToString());

        Assert.Equal("not_configured", checks.GetProperty("stripe").GetString());
        Assert.Equal("not_configured", checks.GetProperty("payTr").GetString());
        Assert.Equal("not_configured", checks.GetProperty("smtp").GetString());
    }

    [Fact]
    public async Task GercekDegerVarken_UcuDeOkGorunur()
    {
        var checks = await StatusAsync(RealValues);

        _output.WriteLine(checks.ToString());

        Assert.Equal("ok", checks.GetProperty("stripe").GetString());
        Assert.Equal("ok", checks.GetProperty("payTr").GetString());
        Assert.Equal("ok", checks.GetProperty("smtp").GetString());
    }

    /// <summary>Anahtarlar dolu ama test modu açıksa para tahsil edilmez.</summary>
    [Fact]
    public async Task PayTrTestModuAcikken_Uyarilir()
    {
        var settings = new Dictionary<string, string?>(RealValues) { ["PayTR:TestMode"] = "true" };
        var checks = await StatusAsync(settings);

        Assert.Equal("test_mode", checks.GetProperty("payTr").GetString());
    }

    /// <summary>Üç PayTR alanından biri eksikse imza üretilemez.</summary>
    [Fact]
    public async Task PayTrAlanlarindanBiriEksikse_YapilandirilmamisSayilir()
    {
        var settings = new Dictionary<string, string?>(RealValues)
        {
            ["PayTR:MerchantSalt"] = "#{PAYTR_MERCHANT_SALT}#"
        };

        var checks = await StatusAsync(settings);

        Assert.Equal("not_configured", checks.GetProperty("payTr").GetString());
    }

    /// <summary>Ekranın kendisi de açılmalı ve durumları basmalı.</summary>
    [Fact]
    public async Task SistemDurumuEkrani_PayTrVeSmtpDurumunuGosterir()
    {
        var response = await CreateClient(Placeholders).GetAsync("/Admin/Health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("PayTR", html, StringComparison.Ordinal);
        Assert.Contains("SMTP", html, StringComparison.Ordinal);
    }
}
