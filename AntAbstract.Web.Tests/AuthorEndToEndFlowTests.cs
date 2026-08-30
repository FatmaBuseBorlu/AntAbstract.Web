using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Sitenin para ve içerik üreten asıl zinciri: bir katılımcının kongreye
/// kaydolup bildirisini göndermesi. Parçaların ayrı ayrı testi vardı ama
/// zincirin tamamı hiç uçtan uca yürütülmüyordu; aradaki bir kopukluk
/// (rolün yükselmemesi, kanonik adrese yönlenirken form gövdesinin
/// kaybolması gibi) fark edilmeden kalabilirdi.
/// </summary>
public sealed class AuthorEndToEndFlowTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "uctan-uca-kurum";
    private const string ConferenceSlug = "uctan-uca-kongre";
    private const string UserId = "uctan-uca-katilimci";

    private static readonly Guid TenantId =
        new("11112222-3333-4444-5555-666677778888");

    private static readonly Guid ConferenceId =
        new("22223333-4444-5555-6666-777788889999");

    private static readonly Guid RegistrationTypeId =
        new("33334444-5555-6666-7777-888899990000");

    private static readonly Guid TopicId =
        new("44445555-6666-7777-8888-99990000aaaa");

    public AuthorEndToEndFlowTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // Katılımcı kaydolmadan önce yalnızca dinleyici.
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Listener");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, UserId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "uctan.uca@test.local",
            NormalizedUserName = "UCTAN.UCA@TEST.LOCAL",
            Email = "uctan.uca@test.local",
            NormalizedEmail = "UCTAN.UCA@TEST.LOCAL",
            FirstName = "Ayşe",
            LastName = "Yılmaz",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Uçtan Uca Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Uçtan Uca Kongresi 2026",
            Slug = ConferenceSlug,
            StartDate = DateTime.Today.AddDays(60),
            EndDate = DateTime.Today.AddDays(62),
            IsRegistrationOpen = true,
            IsSubmissionOpen = true,
            AbstractSubmissionDeadline = DateTime.UtcNow.AddDays(20)
        });

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = RegistrationTypeId,
            ConferenceId = ConferenceId,
            Name = "Bildirili Katılım",
            NameEn = "Presenter Registration",
            Description = "Bildiri göndermek isteyenler içindir.",
            Price = 1000m,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Author"
        });

        db.ConferenceTopics.Add(new ConferenceTopic
        {
            Id = TopicId,
            ConferenceId = ConferenceId,
            Name = "Yapay Zekâ",
            IsActive = true
        });

        db.SaveChanges();
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private async Task<(HttpResponseMessage Response, string Html)> FollowAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var hops = 0;

        while ((int)response.StatusCode is >= 300 and < 400 &&
               response.Headers.Location != null && hops++ < 8)
        {
            response = await _client.GetAsync(response.Headers.Location.ToString());
        }

        return (response, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Katilimci_KaydolurVeBildirisiniGonderir()
    {
        // 1. Kayıt sayfası kayıt türünü listelemeli.
        var registration = await FollowAsync($"/{ConferenceSlug}/registration");

        Assert.Equal(HttpStatusCode.OK, registration.Response.StatusCode);
        Assert.Contains("Bildirili", registration.Html, StringComparison.Ordinal);

        // 2. Checkout formu açılmalı ve kayıt oluşmalı.
        var checkout = await FollowAsync(
            $"/{ConferenceSlug}/registration/checkout/{RegistrationTypeId}");

        Assert.Equal(HttpStatusCode.OK, checkout.Response.StatusCode);

        var posted = await _client.PostAsync(
            $"/{ConferenceSlug}/registration/checkout/{RegistrationTypeId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(checkout.Html),
                ["BillingName"] = "Ayşe Yılmaz",
                ["TaxOffice"] = "",
                ["TaxNumber"] = "",
                ["BillingAddress"] = ""
            }));

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);

        // 3. Kayıt veritabanına düşmeli ve kullanıcı Author rolünü almalı.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var count = await db.Registrations.IgnoreQueryFilters()
                .CountAsync(r => r.AppUserId == UserId && r.ConferenceId == ConferenceId);

            Assert.True(count == 1, "Kongre kaydı oluşmadı.");

            var user = await users.FindByIdAsync(UserId);
            var roles = await users.GetRolesAsync(user!);

            _output.WriteLine($"kayıt={count}, roller={string.Join(",", roles)}");

            Assert.Contains("Author", roles);
        }

        // Üretimde RefreshSignInAsync çerezi yeni rolle yeniliyor; test kancası
        // rolü başlıkla verdiği için aynı etkiyi burada kuruyoruz.
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");

        // 4. Bildiri formu açılmalı.
        var form = await FollowAsync($"/{ConferenceSlug}/submit-abstract");

        Assert.Equal(HttpStatusCode.OK, form.Response.StatusCode);
        Assert.Contains("name=\"Title\"", form.Html, StringComparison.Ordinal);

        // 5. Bildiri gönderilmeli. Form kanonik adrese (kurum slug'ı) gider;
        //    kongre slug'ına POST edilirse yönlendirme olur ve gövde kaybolur.
        //    Bildiri dosyası zorunlu olduğu için multipart gerekiyor.
        var content = new MultipartFormDataContent
        {
            { new StringContent(Token(form.Html)), "__RequestVerificationToken" },
            { new StringContent("Yapay Zekâ Destekli Görüntü İşleme"), "Title" },
            { new StringContent("Bu çalışmada yapay zekâ yöntemleri incelenmiştir."), "AbstractText" },
            { new StringContent("yapay zeka, görüntü işleme"), "Keywords" },
            { new StringContent(TopicId.ToString()), "ConferenceTopicId" },
            { new StringContent("Oral"), "PresentationType" },
            { new StringContent(ConferenceId.ToString()), "ConferenceId" }
        };

        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "SubmissionFile", "bildiri.pdf");

        var submitted = await _client.PostAsync($"/{TenantSlug}/submit-abstract", content);

        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);

        // 6. Bildiri veritabanında olmalı.
        Guid submissionId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var created = await db.Submissions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.ConferenceId == ConferenceId);

            Assert.True(created != null, "Bildiri oluşturulmadı.");
            Assert.Equal("Yapay Zekâ Destekli Görüntü İşleme", created!.Title);
            Assert.Equal(SubmissionStatus.New, created.Status);

            submissionId = created.Id;
        }

        // 7. Bildirilerim listesinde görünmeli, detay ve düzenleme açılmalı.
        var mine = await FollowAsync($"/{ConferenceSlug}/my-submissions");

        Assert.Equal(HttpStatusCode.OK, mine.Response.StatusCode);
        Assert.Contains("Yapay Zek", mine.Html, StringComparison.Ordinal);

        foreach (var url in new[]
        {
            $"/{ConferenceSlug}/my-submissions/{submissionId}",
            $"/{ConferenceSlug}/my-submissions/{submissionId}/edit"
        })
        {
            var page = await FollowAsync(url);
            _output.WriteLine($"{(int)page.Response.StatusCode} {url}");
            Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);
        }
    }

    /// <summary>Yazarın menüsündeki diğer ekranlar da açılmalı.</summary>
    [Theory]
    [InlineData("Ödemelerim", "/payments")]
    [InlineData("Konaklama", "/Accommodation/Index")]
    [InlineData("Program", "/Program/Index")]
    [InlineData("Bildiri Kitabı", "/Dashboard/ProceedingBook")]
    [InlineData("Sertifikalarım", "/Certificates")]
    [InlineData("Mesajlarım", "/Message/Index")]
    public async Task YazarEkranlari_Aciliyor(string ad, string path)
    {
        var page = await FollowAsync($"/{ConferenceSlug}{path}");

        _output.WriteLine($"{(int)page.Response.StatusCode} {ad}");

        Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);
    }
}
