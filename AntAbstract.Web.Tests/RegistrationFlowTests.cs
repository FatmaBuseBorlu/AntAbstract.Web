using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Sitenin en kritik akışı: yeni kullanıcı kaydı. Profil fotoğrafı zorunlu
/// olduğu için yükleme sınırları bu akışı doğrudan engelleyebiliyor.
/// </summary>
public sealed class RegistrationFlowTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string RegisterUrl = "/Identity/Account/Register";

    public RegistrationFlowTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private static byte[] JpegBytes(int size)
    {
        var bytes = new byte[size];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;
        bytes[^2] = 0xFF; bytes[^1] = 0xD9;
        return bytes;
    }

    private async Task<(HttpResponseMessage Response, string Body)> RegisterAsync(
        string email, int photoBytes)
    {
        var page = await _client.GetAsync(RegisterUrl);
        var html = await page.Content.ReadAsStringAsync();

        var token = Regex.Match(
            html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(token), "Kayıt formunda doğrulama jetonu yok.");

        var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Buse"), "Input.FirstName" },
            { new StringContent("Test"), "Input.LastName" },
            { new StringContent("12345678901"), "Input.IdentityNumber" },
            { new StringContent(email), "Input.Email" },
            { new StringContent("Test Üniversitesi"), "Input.University" },
            { new StringContent("Dr."), "Input.Title" },
            { new StringContent("Mühendislik"), "Input.Faculty" },
            { new StringContent("Bilgisayar"), "Input.Department" },
            { new StringContent("Deneme123!"), "Input.Password" },
            { new StringContent("Deneme123!"), "Input.ConfirmPassword" },
            { new StringContent("true"), "Input.TermsAccepted" },
            { new StringContent("true"), "Input.KvkkAccepted" }
        };

        var photo = new ByteArrayContent(JpegBytes(photoBytes));
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(photo, "Input.ProfileImage", "profil.jpg");

        var response = await _client.PostAsync(RegisterUrl, form);
        var body = await response.Content.ReadAsStringAsync();

        return (response, body);
    }

    private static void DumpErrors(ITestOutputHelper output, string body)
    {
        foreach (Match m in Regex.Matches(
                     body, @"field-validation-error[^>]*>([^<]+)<"))
        {
            output.WriteLine("  alan hatası: " + System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim());
        }

        foreach (Match m in Regex.Matches(body, @"validation-summary-errors[\s\S]{0,600}?</div>"))
        {
            foreach (Match li in Regex.Matches(m.Value, @"<li>(.*?)</li>"))
            {
                output.WriteLine("  özet hatası: " + System.Net.WebUtility.HtmlDecode(li.Groups[1].Value).Trim());
            }
        }
    }

    [Fact]
    public async Task RegisterPage_Opens()
    {
        var response = await _client.GetAsync(RegisterUrl);

        _output.WriteLine($"{(int)response.StatusCode} kayıt sayfası");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Telefon boyutunda (5 MB) fotoğrafla kayıt tamamlanmalı ve kullanıcı
    /// gerçekten oluşmalı.
    /// </summary>
    [Fact]
    public async Task Register_CreatesUser_WithPhoneSizedPhoto()
    {
        var email = $"kayit-{Guid.NewGuid():N}@test.local";

        var (response, body) = await RegisterAsync(email, 5 * 1024 * 1024);

        _output.WriteLine($"{(int)response.StatusCode} kayıt gönderimi");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            DumpErrors(_output, body);
        }

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var created = await users.FindByEmailAsync(email);

        Assert.True(created != null, "Kayıt tamamlandı ama kullanıcı oluşmadı.");
        Assert.Equal("Buse", created!.FirstName);
    }

    /// <summary>
    /// Sınırın üstündeki fotoğraf reddedilmeli ama kullanıcı da oluşmamalı.
    /// </summary>
    [Fact]
    public async Task Register_RejectsOversizedPhoto_WithoutCreatingUser()
    {
        var email = $"buyuk-{Guid.NewGuid():N}@test.local";

        var (response, _) = await RegisterAsync(email, 21 * 1024 * 1024);

        _output.WriteLine($"{(int)response.StatusCode} büyük fotoğraf");

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        Assert.Null(await users.FindByEmailAsync(email));
    }
}
