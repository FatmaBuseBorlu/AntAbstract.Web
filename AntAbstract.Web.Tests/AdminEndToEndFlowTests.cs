using System.Net;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Yönetici zinciri: bildiriye karar verilir, oturum oluşturulur, kabul
/// edilen bildiri programa yerleştirilir. Ekranlar tek tek test ediliyordu
/// ama bu zincir uçtan uca yürütülmüyordu.
/// </summary>
public sealed class AdminEndToEndFlowTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "yonetim-akis-kurum";

    private static readonly Guid TenantId = new("dddd1111-2222-3333-4444-555566667777");
    private static readonly Guid ConferenceId = new("eeee1111-2222-3333-4444-555566667777");
    private static readonly Guid SubmissionId = new("ffff1111-2222-3333-4444-555566667777");

    public AdminEndToEndFlowTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = "yonetim-akis-yazar",
            UserName = "ya@test.local",
            NormalizedUserName = "YA@TEST.LOCAL",
            Email = "ya@test.local",
            NormalizedEmail = "YA@TEST.LOCAL",
            FirstName = "Yönetim",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Yönetim Akış Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Yönetim Akış Kongresi",
            Slug = "yonetim-akis-kongre",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32),
            IsSubmissionOpen = true,
            IsRegistrationOpen = true
        });

        db.Submissions.Add(new Submission
        {
            Id = SubmissionId,
            ConferenceId = ConferenceId,
            TenantId = TenantId,
            AuthorId = "yonetim-akis-yazar",
            Title = "Karar Verilecek Bildiri",
            Abstract = "Özet",
            Keywords = "test",
            PresentationType = "Oral",
            Status = SubmissionStatus.UnderReview,
            CreatedDate = DateTime.UtcNow
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

        return (response, System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Yonetici_KararVerirOturumOlusturupBildiriyiProgramaKoyar()
    {
        // 1. Karar ekranı açılmalı ve karar bildiriyi kabul etmeli.
        var decision = await FollowAsync($"/{TenantSlug}/Admin/Decision?conferenceId={ConferenceId}");

        Assert.Equal(HttpStatusCode.OK, decision.Response.StatusCode);

        var decided = await _client.PostAsync(
            $"/{TenantSlug}/Admin/Decision/MakeDecision",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(decision.Html),
                ["submissionId"] = SubmissionId.ToString(),
                ["decision"] = "Accept",
                ["note"] = "Kabul edildi."
            }));

        Assert.Equal(HttpStatusCode.Redirect, decided.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var submission = await db.Submissions.IgnoreQueryFilters().FirstAsync(s => s.Id == SubmissionId);

            Assert.Equal(SubmissionStatus.Accepted, submission.Status);
        }

        // 2. Oturum oluşturulmalı.
        var form = await FollowAsync($"/{TenantSlug}/Admin/Session/Create?conferenceId={ConferenceId}");

        Assert.Equal(HttpStatusCode.OK, form.Response.StatusCode);

        var created = await _client.PostAsync(
            $"/{TenantSlug}/Admin/Session/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(form.Html),
                ["Slug"] = TenantSlug,
                ["ConferenceId"] = ConferenceId.ToString(),
                ["Title"] = "Açılış Oturumu",
                ["SessionDate"] = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
                ["StartTime"] = "09:00",
                ["EndTime"] = "10:30",
                ["Location"] = "A Salonu",
                ["SortOrder"] = "1"
            }));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        Guid sessionId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.Sessions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.ConferenceId == ConferenceId);

            Assert.True(session != null, "Oturum oluşturulmadı.");
            Assert.Equal("Açılış Oturumu", session!.Title);

            sessionId = session.Id;
        }

        // 3. Kabul edilen bildiri oturuma yerleştirilmeli.
        var manage = await FollowAsync($"/{TenantSlug}/Admin/Session/Manage/{sessionId}");

        Assert.Equal(HttpStatusCode.OK, manage.Response.StatusCode);

        var added = await _client.PostAsync(
            $"/{TenantSlug}/Admin/Session/AddSubmission",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(manage.Html),
                ["sessionId"] = sessionId.ToString(),
                ["submissionId"] = SubmissionId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var submission = await db.Submissions.IgnoreQueryFilters().FirstAsync(s => s.Id == SubmissionId);

            Assert.True(submission.SessionId.HasValue, "Bildiri oturuma yerleşmedi.");
            _output.WriteLine($"bildiri oturumda: {submission.SessionId}");
        }
    }

    /// <summary>Yönetici ekranları açılmalı; Yaka Kartı her açılışta 500 veriyordu.</summary>
    [Theory]
    [InlineData("Bildiri Kitabı", "/Admin/ProceedingBook")]
    [InlineData("Raporlar", "/Admin/Reports")]
    [InlineData("Sertifikalar", "/Admin/Certificates")]
    [InlineData("Yaka Kartı", "/Admin/Attendance/Badges")]
    [InlineData("Katılımcı Listesi", "/Admin/Attendance")]
    [InlineData("Anket Sonuçları", "/Admin/SurveyResults")]
    [InlineData("Değerlendirme Kriterleri", "/Admin/ReviewCriteria")]
    [InlineData("Toplu E-posta", "/Admin/Broadcast")]
    public async Task YoneticiEkranlari_Aciliyor(string ad, string path)
    {
        var page = await FollowAsync($"/{TenantSlug}{path}?conferenceId={ConferenceId}");

        _output.WriteLine($"{(int)page.Response.StatusCode} {ad}");

        Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);
    }
}
