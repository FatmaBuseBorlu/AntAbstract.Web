using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Yazar panelindeki sol menü bağlantılarını gerçek bir Yazar kimliğiyle gezer.
///
/// Beklenti: hiçbiri 500 (kırık sayfa) ya da 403 (menüde görünüp açılmayan
/// bağlantı) dönmemeli. Yönlendirme kabul edilir — bazı sayfalar önce kongre
/// seçimi ister.
/// </summary>
public sealed class AuthorMenuTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "yazar-menu-kurum";
    private const string ConfSlug = "yazar-menu-kongresi";
    private const string AuthorId = "yazar-menu-kullanici";

    private static readonly Guid TenantId =
        new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static readonly Guid ConferenceId =
        new("ffffffff-ffff-ffff-ffff-ffffffffffff");

    public AuthorMenuTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AuthorId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = AuthorId,
            UserName = AuthorId + "@antabstract.local",
            NormalizedUserName = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            Email = AuthorId + "@antabstract.local",
            NormalizedEmail = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            FirstName = "Yazar",
            LastName = "Test",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Yazar Menü Kurumu" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Yazar Menü Kongresi",
            Slug = "yazar-menu-kongresi",
            StartDate = DateTime.Today.AddDays(20),
            EndDate = DateTime.Today.AddDays(22)
        });

        // Yazarın kongreye kaydı olmadan sayfaların çoğu erişim reddiyle
        // yönlendiriyor; gerçek yazar deneyimini ölçmek için kayıt ekliyoruz.
        var typeId = Guid.NewGuid();

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = typeId,
            ConferenceId = ConferenceId,
            Name = "Akademisyen",
            Description = "Test",
            Price = 0,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Author"
        });

        db.Registrations.Add(new Registration
        {
            Id = Guid.NewGuid(),
            AppUserId = AuthorId,
            ConferenceId = ConferenceId,
            RegistrationTypeId = typeId,
            RegistrationDate = DateTime.UtcNow,
            IsPaid = false,
            Amount = 0
        });

        db.SaveChanges();
    }

    // Program sayfası bilerek listede değil: sorgusu oturumları TimeSpan
    // alanına göre sıralıyor ve SQLite bunu çeviremiyor. SQL Server çevirebiliyor
    // (ayrıca doğrulandı), yani üretimde sorun yok — burada tutmak yanlış alarm üretirdi.
    public static TheoryData<string, string> AuthorMenuLinks() => new()
    {
        { "Kongrelerim",     "/Dashboard/MyConferences" },
        { "Bildiri Gönder",  $"/{ConfSlug}/submit-abstract" },
        { "Bildirilerim",    $"/{ConfSlug}/my-submissions" },
        { "Bildiri Kitabı",  $"/{ConfSlug}/Dashboard/ProceedingBook" },
        { "Ödemelerim",      $"/{ConfSlug}/payments" },
        { "Konaklama",       $"/{ConfSlug}/Accommodation/Index" },
        { "Sertifikalarım",  "/Certificates" },
    };

    /// <summary>
    /// Kayıtlı yazar için Bildiri Kitabı doğrudan açılmalı. Aksiyon eskiden
    /// yalnızca oturumdaki seçili kongreye bakıyordu; adres bir kongreye
    /// işaret etse bile "önce kongre seçin" deyip Kongrelerim'e atıyordu.
    /// </summary>
    [Fact]
    public async Task ProceedingBook_OpensDirectly_ForRegisteredAuthor()
    {
        var response = await _client.GetAsync($"/{ConfSlug}/Dashboard/ProceedingBook");

        _output.WriteLine($"{(int)response.StatusCode} bildiri kitabı");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AuthorMenuLinks))]
    public async Task AuthorMenuLink_IsReachable(string label, string url)
    {
        var response = await _client.GetAsync(url);
        var code = (int)response.StatusCode;

        _output.WriteLine($"{code} {label,-16} {url}" +
                          (response.Headers.Location is null ? "" : $"  -> {response.Headers.Location}"));

        Assert.False(code == 500, $"{label} sayfası hata veriyor (500).");
        Assert.False(code == 403, $"{label} menüde görünüyor ama açılmıyor (403).");
        Assert.False(code == 404, $"{label} bağlantısı bulunamadı (404).");
    }
}
