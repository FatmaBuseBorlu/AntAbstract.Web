using System.Net;
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
/// Hakem değerlendirme zinciri: yönetici bildiriyi hakeme atar, hakem
/// görevini görür ve değerlendirmesini gönderir, sonuç karar ekranına
/// düşer. Ekranlar tek tek test ediliyordu ama zincir hiç uçtan uca
/// yürütülmüyordu.
/// </summary>
public sealed class RefereeEndToEndFlowTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _referee;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "hakem-akis-kurum";
    private const string ConferenceSlug = "hakem-akis-kongre";
    private const string RefereeId = "hakem-akis-hakem";
    private const string AuthorId = "hakem-akis-yazar";

    private static readonly Guid TenantId =
        new("aaaa1111-bbbb-2222-cccc-333344445555");

    private static readonly Guid ConferenceId =
        new("bbbb1111-cccc-2222-dddd-333344445555");

    private static readonly Guid SubmissionId =
        new("cccc1111-dddd-2222-eeee-333344445555");

    public RefereeEndToEndFlowTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        _admin = factory.CreateClient(new() { AllowAutoRedirect = false });

        _referee = factory.CreateClient(new() { AllowAutoRedirect = false });
        _referee.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Referee");
        _referee.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, RefereeId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        foreach (var (id, mail, ad, soyad) in new[]
        {
            (RefereeId, "hakem.akis@test.local", "Ali", "Çelik"),
            (AuthorId, "yazar.akis2@test.local", "Ayşe", "Şahin")
        })
        {
            db.Users.Add(new AppUser
            {
                Id = id,
                UserName = mail,
                NormalizedUserName = mail.ToUpperInvariant(),
                Email = mail,
                NormalizedEmail = mail.ToUpperInvariant(),
                FirstName = ad,
                LastName = soyad,
                SecurityStamp = Guid.NewGuid().ToString()
            });
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Hakem Akış Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Hakem Akış Kongresi",
            Slug = ConferenceSlug,
            StartDate = DateTime.Today.AddDays(50),
            EndDate = DateTime.Today.AddDays(52),
            IsSubmissionOpen = true,
            IsRegistrationOpen = true
        });

        db.Submissions.Add(new Submission
        {
            Id = SubmissionId,
            ConferenceId = ConferenceId,
            TenantId = TenantId,
            AuthorId = AuthorId,
            Title = "Hakeme Atanan Bildiri",
            Abstract = "Özet metni",
            Keywords = "test",
            PresentationType = "Oral",
            Status = SubmissionStatus.New,
            CreatedDate = DateTime.UtcNow
        });

        db.SaveChanges();
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private static async Task<(HttpResponseMessage Response, string Html)> FollowAsync(
        HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var hops = 0;

        while ((int)response.StatusCode is >= 300 and < 400 &&
               response.Headers.Location != null && hops++ < 8)
        {
            response = await client.GetAsync(response.Headers.Location.ToString());
        }

        return (response, System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    private async Task<int> EnsureAssignmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Üretimde bu roller DbSeeder/DbInitializer ile kuruluyor.
        foreach (var name in new[] { "Referee", "Author", "Listener", "Admin" })
        {
            if (!await roles.RoleExistsAsync(name))
            {
                await roles.CreateAsync(new IdentityRole(name));
            }
        }

        var referee = await users.FindByIdAsync(RefereeId);

        if (!await users.IsInRoleAsync(referee!, "Referee"))
        {
            await users.AddToRoleAsync(referee!, "Referee");
        }

        var existing = await db.ReviewAssignments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.SubmissionId == SubmissionId);

        if (existing != null)
        {
            return existing.Id;
        }

        var assignment = new ReviewAssignment
        {
            SubmissionId = SubmissionId,
            ReviewerId = RefereeId,
            AssignedDate = DateTime.UtcNow
        };

        db.ReviewAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return assignment.Id;
    }

    [Fact]
    public async Task Hakem_GorevidiGorurVeDegerlendirmesiniGonderir()
    {
        var assignmentId = await EnsureAssignmentAsync();

        // 1. Hakem görevini listesinde görmeli.
        var tasks = await FollowAsync(_referee, $"/{TenantSlug}/Review/Index");

        Assert.Equal(HttpStatusCode.OK, tasks.Response.StatusCode);
        Assert.Contains("Hakeme Atanan Bildiri", tasks.Html, StringComparison.Ordinal);

        // 2. Değerlendirme formu açılmalı.
        var form = await FollowAsync(_referee, $"/{TenantSlug}/Review/Evaluate/{assignmentId}");

        Assert.Equal(HttpStatusCode.OK, form.Response.StatusCode);

        // 3. Değerlendirme kanonik adrese gönderilmeli; kongre slug'ına
        //    POST edilirse yönlendirme olur ve form gövdesi kaybolur.
        var submitted = await _referee.PostAsync(
            $"/{TenantSlug}/Review/Evaluate",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(form.Html),
                ["ReviewAssignmentId"] = assignmentId.ToString(),
                ["CommentsToAuthor"] = "Çalışma özgün ve iyi kurgulanmış.",
                ["Recommendation"] = "Accept",
                ["Score"] = "85",
                ["ScoreOriginality"] = "9",
                ["ScoreMethodology"] = "8",
                ["ScorePresentation"] = "8",
                ["ScoreRelevance"] = "9"
            }));

        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);

        // 4. Değerlendirme veritabanına düşmeli.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var assignment = await db.ReviewAssignments.IgnoreQueryFilters()
                .Include(x => x.Review)
                .FirstAsync(x => x.Id == assignmentId);

            Assert.True(assignment.Review != null, "Değerlendirme kaydedilmedi.");
            Assert.Equal(85, assignment.Review!.Score);
            Assert.Equal("Accept", assignment.Review.Recommendation);

            _output.WriteLine($"puan={assignment.Review.Score} öneri={assignment.Review.Recommendation}");
        }

        // 5. Sonuç yöneticinin karar ekranına düşmeli.
        var decision = await FollowAsync(
            _admin, $"/{TenantSlug}/Admin/Decision?conferenceId={ConferenceId}");

        Assert.Equal(HttpStatusCode.OK, decision.Response.StatusCode);
        Assert.Contains("85", decision.Html, StringComparison.Ordinal);
    }

    /// <summary>Yöneticinin hakem ekranları açılmalı.</summary>
    [Theory]
    [InlineData("Hakemler", "/Admin/Referee")]
    [InlineData("E-posta ile davet", "/Admin/Referee/InviteByEmail")]
    [InlineData("Davetler", "/Admin/Referee/Invitations")]
    [InlineData("Hakem atama", "/Admin/Assignment")]
    [InlineData("Hakem iş yükü", "/Admin/Assignment/Workload")]
    public async Task YoneticiHakemEkranlari_Aciliyor(string ad, string path)
    {
        var page = await FollowAsync(_admin, $"/{TenantSlug}{path}?conferenceId={ConferenceId}");

        _output.WriteLine($"{(int)page.Response.StatusCode} {ad}");

        Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);
    }

    /// <summary>Hakemin kendi ekranları açılmalı.</summary>
    [Theory]
    [InlineData("Uzmanlık alanlarım", "/Review/Interests")]
    [InlineData("Müsaitlik", "/Review/Availability")]
    [InlineData("Çıkar çatışmaları", "/Review/Conflicts")]
    [InlineData("Hakem rehberi", "/Review/Guidelines")]
    public async Task HakemEkranlari_Aciliyor(string ad, string path)
    {
        var page = await FollowAsync(_referee, $"/{TenantSlug}{path}");

        _output.WriteLine($"{(int)page.Response.StatusCode} {ad}");

        Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);
    }
}
