using System.Net;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kayıtta e-posta hiç doğrulanmıyordu (EmailConfirmed = true). Artık SMTP
/// ayarlıyken doğrulanmamış hesapla giriş yapılamıyor; SMTP yokken doğrulama
/// istenmiyor (e-posta gidemez, yoksa kimse giriş yapamazdı — canlıda yaşandı).
/// Kayıt sonrası sayfa doğrulama bağlantısını ekrana basmıyor.
/// </summary>
public sealed class EmailConfirmationTests : IClassFixture<AuthenticatedTestFactory>
{
    private const string Password = "Sifre1234";

    private readonly WebApplicationFactory<Program> factory;

    public EmailConfirmationTests(AuthenticatedTestFactory baseFactory)
    {
        factory = baseFactory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:SmtpServer"] = "smtp.test.local",
                ["Email:Username"] = "test",
                ["Email:Password"] = "test"
            })));
        _noSmtp = baseFactory;
    }

    private readonly WebApplicationFactory<Program> _noSmtp;

    private async Task CreateUserAsync(string email, bool confirmed, WebApplicationFactory<Program>? on = null)
    {
        using var scope = (on ?? factory).Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (await users.FindByEmailAsync(email) != null)
            return;

        var result = await users.CreateAsync(
            new AppUser { UserName = email, Email = email, EmailConfirmed = confirmed }, Password);
        Assert.True(result.Succeeded);
    }

    private async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email)
    {
        var page = await client.GetStringAsync("/Identity/Account/Login");
        var token = Regex.Match(
            page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        return await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.Email"] = email,
                ["Input.Password"] = Password
            }));
    }

    [Fact]
    public async Task UnconfirmedUser_CannotSignIn()
    {
        const string email = "dogrulanmamis@antabstract.local";
        await CreateUserAsync(email, confirmed: false);

        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await LoginAsync(client, email);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("doğrulanmadı", body);
    }

    [Fact]
    public async Task WithoutSmtp_UnconfirmedUser_CanSignIn()
    {
        // SMTP yokken doğrulama e-postası gidemez; takılı kalan hesaplar girebilmeli.
        const string email = "smtpsiz-dogrulanmamis@antabstract.local";
        await CreateUserAsync(email, confirmed: false, on: _noSmtp);

        var client = _noSmtp.CreateClient(new() { AllowAutoRedirect = false });
        var response = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmedUser_SignsIn()
    {
        const string email = "dogrulanmis@antabstract.local";
        await CreateUserAsync(email, confirmed: true);

        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task RegisterConfirmation_DoesNotRevealLinkOrAccount()
    {
        const string email = "sizinti@antabstract.local";
        await CreateUserAsync(email, confirmed: false);

        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var known = await client.GetAsync($"/Identity/Account/RegisterConfirmation?email={email}");
        var unknown = await client.GetAsync("/Identity/Account/RegisterConfirmation?email=yok@x.local");
        var body = await known.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.DoesNotContain("ConfirmEmail?", body);
    }
}
