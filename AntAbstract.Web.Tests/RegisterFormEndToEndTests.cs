using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kayıt formunu bir kullanıcı gibi (fotoğrafla, multipart) gönderir.
/// Canlıda SMTP boşken e-posta doğrulaması zorunlu olduğu için yeni kayıt
/// olanlar giriş yapamıyordu; şifre kuralları da formda 6, sunucuda 8
/// karakterdi ve hata İngilizce dönüyordu.
/// </summary>
public sealed class RegisterFormEndToEndTests(AuthenticatedTestFactory factory) : IClassFixture<AuthenticatedTestFactory>
{
    // 1x1 PNG
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private async Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, string email, string password)
    {
        var page = await client.GetStringAsync("/Identity/Account/Register");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Deniz"), "Input.FirstName" },
            { new StringContent("Kaya"), "Input.LastName" },
            { new StringContent("12345678901"), "Input.IdentityNumber" },
            { new StringContent(email), "Input.Email" },
            { new StringContent("__OTHER__"), "Input.University" },
            { new StringContent("Örnek Üniversitesi"), "Input.OtherUniversity" },
            { new StringContent("Dr."), "Input.Title" },
            { new StringContent("__OTHER__"), "Input.Faculty" },
            { new StringContent("Fen Fakültesi"), "Input.OtherFaculty" },
            { new StringContent("__OTHER__"), "Input.Department" },
            { new StringContent("Biyoloji"), "Input.OtherDepartment" },
            { new StringContent(password), "Input.Password" },
            { new StringContent(password), "Input.ConfirmPassword" },
            { new StringContent("true"), "Input.TermsAccepted" },
            { new StringContent("true"), "Input.KvkkAccepted" },
            { new StringContent("false"), "Input.MarketingConsent" },
        };

        var image = new ByteArrayContent(Png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "Input.ProfileImage", "foto.png");

        return await client.PostAsync("/Identity/Account/Register", form);
    }

    [Fact]
    public async Task WithoutSmtp_NewUserIsSignedInAndConfirmed()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        const string email = "yeni-kayit@antabstract.local";

        var response = await PostRegisterAsync(client, email, "Sifre1234");
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(response.StatusCode == HttpStatusCode.Redirect,
            "Kayıt formu reddedildi: " + string.Join(" | ",
                Regex.Matches(body, "field-validation-error[^>]*>([^<]+)<").Select(m => m.Groups[1].Value)));
        Assert.DoesNotContain("RegisterConfirmation", response.Headers.Location?.ToString() ?? "");

        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
    }

    [Fact]
    public async Task WeakPassword_ShowsTurkishRuleNotEnglish()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // 8 karakter ama büyük harf yok: form geçer, sunucu kuralı reddeder.
        var response = await PostRegisterAsync(client, "zayif-sifre@antabstract.local", "sifre1234");
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("büyük harf", body);
        Assert.DoesNotContain("Passwords must have", body);
    }
}
