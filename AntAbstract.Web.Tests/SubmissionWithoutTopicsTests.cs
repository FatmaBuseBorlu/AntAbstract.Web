using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Bildiri konusu tanımlanmamış kongrede yazar tıkanıyordu: ekran "bu kongre
/// için henüz bildiri konusu tanımlanmamış" diyor, seçilecek bir şey
/// sunmuyor, ama denetleyici konuyu yine de zorunlu tutuyordu. Yazar formu
/// eksiksiz doldursa bile bildiriyi hiçbir şekilde gönderemiyordu.
///
/// Doğru davranış: konu yoksa alan isteğe bağlı, yönetici konu ekleyince
/// tekrar zorunlu.
/// </summary>
public sealed class SubmissionWithoutTopicsTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "konusuz-kurum";
    private const string ConferenceSlug = "konusuz-kongre";
    private const string UserId = "konusuz-yazar";

    private static readonly Guid TenantId = new("cccc3333-0000-0000-0000-000000000001");
    private static readonly Guid ConferenceId = new("cccc3333-0000-0000-0000-000000000002");

    public SubmissionWithoutTopicsTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
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
            UserName = "konusuz@test.local",
            NormalizedUserName = "KONUSUZ@TEST.LOCAL",
            Email = "konusuz@test.local",
            NormalizedEmail = "KONUSUZ@TEST.LOCAL",
            FirstName = "Kübra",
            LastName = "Aslan",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Konusuz Üniversitesi" });

        // Bilerek hiç ConferenceTopic eklenmiyor.
        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Konusuz Kongre 2026",
            Slug = ConferenceSlug,
            StartDate = DateTime.Today.AddDays(60),
            EndDate = DateTime.Today.AddDays(62),
            IsRegistrationOpen = true,
            IsSubmissionOpen = true,
            AbstractSubmissionDeadline = DateTime.UtcNow.AddDays(20)
        });

        var regTypeId = Guid.NewGuid();

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = regTypeId,
            ConferenceId = ConferenceId,
            Name = "Bildirili Katılım",
            Price = 0m,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Author"
        });

        db.Registrations.Add(new Registration
        {
            Id = Guid.NewGuid(),
            ConferenceId = ConferenceId,
            RegistrationTypeId = regTypeId,
            AppUserId = UserId,
            Status = RegistrationStatus.Confirmed
        });

        db.SaveChanges();
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private async Task<string> OpenFormAsync()
    {
        var response = await _client.GetAsync($"/{ConferenceSlug}/submit-abstract");
        var hops = 0;

        while ((int)response.StatusCode is >= 300 and < 400 &&
               response.Headers.Location != null && hops++ < 8)
        {
            response = await _client.GetAsync(response.Headers.Location.ToString());
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static MultipartFormDataContent BuildSubmission(string token, string? topicId)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Limon veya limon suyu"), "Title" },
            { new StringContent("Bu çalışmada limon suyunun etkileri incelenmiştir."), "AbstractText" },
            { new StringContent("limon, suyu"), "Keywords" },
            { new StringContent("Oral"), "PresentationType" },
            { new StringContent(ConferenceId.ToString()), "ConferenceId" }
        };

        if (topicId != null)
        {
            content.Add(new StringContent(topicId), "ConferenceTopicId");
        }

        var file = new ByteArrayContent(
            Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "SubmissionFile", "bildiri.pdf");

        return content;
    }

    [Fact]
    public async Task KonuTanimliDegilse_BildiriGonderilebiliyor()
    {
        var html = await OpenFormAsync();

        Assert.Contains("henüz bildiri konusu tanımlanmamış", html, StringComparison.Ordinal);

        var posted = await _client.PostAsync(
            $"/{TenantSlug}/submit-abstract",
            BuildSubmission(Token(html), topicId: null));

        _output.WriteLine($"gönderim sonucu: {(int)posted.StatusCode}");

        if (posted.StatusCode != HttpStatusCode.Redirect)
        {
            var body = WebUtility.HtmlDecode(await posted.Content.ReadAsStringAsync());
            foreach (Match m in Regex.Matches(body, "text-danger[^>]*>\\s*([^<]{3,150})"))
            {
                _output.WriteLine("HATA: " + m.Groups[1].Value.Trim());
            }
            foreach (Match m in Regex.Matches(body, "validation-summary[\\s\\S]{0,400}"))
            {
                _output.WriteLine("OZET: " + Regex.Replace(m.Value, "<[^>]+>", " ").Trim()[..Math.Min(300, Regex.Replace(m.Value, "<[^>]+>", " ").Trim().Length)]);
            }
        }

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var submission = await db.Submissions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ConferenceId == ConferenceId);

        Assert.NotNull(submission);
        Assert.Null(submission!.ConferenceTopicId);
    }

    /// <summary>
    /// Konu tanımlıysa eskisi gibi zorunlu kalmalı; aksi halde bu düzeltme
    /// konu seçimini her kongrede isteğe bağlı hale getirmiş olurdu.
    /// </summary>
    [Fact]
    public async Task KonuTanimliysa_KonuHalaZorunlu()
    {
        var tenantId = new Guid("cccc3333-0000-0000-0000-000000000011");
        var conferenceId = new Guid("cccc3333-0000-0000-0000-000000000012");
        const string slug = "konulu-kongre";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!db.Tenants.IgnoreQueryFilters().Any(t => t.Id == tenantId))
            {
                db.Tenants.Add(new Tenant { Id = tenantId, Slug = "konulu-kurum", Name = "Konulu Üniversitesi" });

                db.Conferences.Add(new Conference
                {
                    Id = conferenceId,
                    TenantId = tenantId,
                    Title = "Konulu Kongre 2026",
                    Slug = slug,
                    StartDate = DateTime.Today.AddDays(60),
                    EndDate = DateTime.Today.AddDays(62),
                    IsRegistrationOpen = true,
                    IsSubmissionOpen = true,
                    AbstractSubmissionDeadline = DateTime.UtcNow.AddDays(20)
                });

                db.ConferenceTopics.Add(new ConferenceTopic
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    Name = "Moleküler Biyoloji",
                    IsActive = true
                });

                var regTypeId = Guid.NewGuid();

                db.RegistrationTypes.Add(new RegistrationType
                {
                    Id = regTypeId,
                    ConferenceId = conferenceId,
                    Name = "Bildirili Katılım",
                    Price = 0m,
                    Currency = "TRY",
                    IsActive = true,
                    RoleName = "Author"
                });

                db.Registrations.Add(new Registration
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    RegistrationTypeId = regTypeId,
                    AppUserId = UserId,
                    Status = RegistrationStatus.Confirmed
                });

                db.SaveChanges();
            }
        }

        var response = await _client.GetAsync($"/{slug}/submit-abstract");
        var hops = 0;

        while ((int)response.StatusCode is >= 300 and < 400 &&
               response.Headers.Location != null && hops++ < 8)
        {
            response = await _client.GetAsync(response.Headers.Location.ToString());
        }

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        var content = BuildSubmission(Token(html), topicId: null);
        content.Add(new StringContent(conferenceId.ToString()), "ConferenceIdOverride");

        var posted = await _client.PostAsync("/konulu-kurum/submit-abstract", content);

        _output.WriteLine($"konulu kongre gönderim sonucu: {(int)posted.StatusCode}");

        // Konu seçilmediği için form geri gelmeli, yönlendirme olmamalı.
        Assert.NotEqual(HttpStatusCode.Redirect, posted.StatusCode);
    }
}
