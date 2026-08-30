using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests.Smoke;

/// <summary>
/// Genel rotalar için tarama vardı ama oturum açmış rollerin ekranları
/// taranmıyordu. Bu oturumda bulunan hataların çoğu (Yaka Kartı 500'ü,
/// ödeme rota çakışması, slug'sız adreslerde boşalan listeler) tam da
/// buralardaydı.
///
/// Tarama 500'e bakıyor: 302/403 anlamlı olabilir (yönlendirme, yetki),
/// ama 500 her zaman hatadır.
/// </summary>
public sealed class AuthenticatedRouteSweepTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "tarama-kurum";

    private static readonly Guid TenantId = new("abcd0000-1111-2222-3333-444455556666");
    private static readonly Guid ConferenceId = new("abcd0000-1111-2222-3333-444455556667");

    public AuthenticatedRouteSweepTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Tarama Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Tarama Kongresi 2026",
            Slug = "tarama-kongre",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32),
            IsRegistrationOpen = true,
            IsSubmissionOpen = true,
            City = "Ankara",
            Country = "Türkiye"
        });

        db.SaveChanges();
    }

    private HttpClient Client(string role)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, $"tarama-{role.ToLowerInvariant()}");
        return client;
    }

    private async Task SweepAsync(string role, IEnumerable<string> routes)
    {
        var client = Client(role);
        var failures = new List<string>();

        foreach (var route in routes)
        {
            HttpResponseMessage response;

            try
            {
                response = await client.GetAsync(route);
            }
            catch (Exception ex)
            {
                failures.Add($"{route} -> istisna: {ex.GetType().Name}");
                continue;
            }

            var code = (int)response.StatusCode;

            _output.WriteLine($"{code}  {route}");

            if (code >= 500)
            {
                failures.Add($"{route} -> {code}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{role} rolünde {failures.Count} ekran sunucu hatası verdi:\n" +
            string.Join("\n", failures));
    }

    public static TheoryData<string, string> AdminRoutes()
    {
        string[] paths =
        [
            "/Admin/Assignment", "/Admin/Assignment/Workload", "/Admin/Attendance",
            "/Admin/Attendance/Badges", "/Admin/Attendance/Scan", "/Admin/AuditLogs",
            "/Admin/Broadcast", "/Admin/ConferenceFlow", "/Admin/ConferenceFlow/ProgramSessions",
            "/Admin/ConferenceFlow/RegistrationsAndPayments", "/Admin/ConferenceTopics",
            "/Admin/ConferenceTopics/Create", "/Admin/ConferenceWizard/Step1", "/Admin/Conferences",
            "/Admin/Conferences/Create", "/Admin/Decision", "/Admin/Health", "/Admin/PageBlocks",
            "/Admin/PageBlocks/Create", "/Admin/Payments/FinanceSummary", "/Admin/PermissionMatrix",
            "/Admin/ProceedingBook", "/Admin/Referee", "/Admin/Referee/Create",
            "/Admin/Referee/Invitations", "/Admin/RegistrationTypes", "/Admin/RegistrationTypes/Create",
            "/Admin/Registrations", "/Admin/Reports", "/Admin/Reports/Referees",
            "/Admin/Reports/System", "/Admin/Reports/UnregisteredAuthors", "/Admin/ReviewCriteria",
            "/Admin/ReviewCriteria/Create", "/Admin/SelectConference", "/Admin/Session",
            "/Admin/Session/Create", "/Admin/Speakers", "/Admin/Speakers/Create", "/Admin/Sponsors",
            "/Admin/Sponsors/Create", "/Admin/Submissions", "/Admin/Submissions/Create",
            "/Admin/Submissions/Incomplete", "/Admin/SurveyResults", "/Admin/SystemReports",
            "/Admin/Users", "/Admin/Users/LoginHistory", "/Admin/Users/ManageRoles",
            "/Admin/Website", "/Admin/Website/Create", "/Admin/EmailTemplates",
            "/Admin/EmailTemplates/SendLog", "/Admin/CentralVitrin"
        ];

        var data = new TheoryData<string, string>();

        foreach (var role in new[] { "Admin", "SuperAdmin" })
        {
            data.Add(role, string.Join('|', paths));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public Task YoneticiEkranlari_SunucuHatasiVermiyor(string role, string joined) =>
        SweepAsync(role, joined.Split('|'));

    [Fact]
    public Task YazarEkranlari_SunucuHatasiVermiyor() =>
        SweepAsync("Author",
        [
            "/Dashboard", "/Dashboard/MyConferences", "/Dashboard/ProceedingBook",
            "/Certificates", "/Accommodation", "/Accommodation/MyBooking",
            "/Payment/My", "/Proceedings/Index", "/Notification/Index",
            $"/{TenantSlug}/Dashboard", $"/{TenantSlug}/my-submissions",
            $"/{TenantSlug}/submit-abstract", $"/{TenantSlug}/payments",
            $"/{TenantSlug}/program", $"/{TenantSlug}/Accommodation"
        ]);

    [Fact]
    public Task HakemEkranlari_SunucuHatasiVermiyor() =>
        SweepAsync("Referee",
        [
            "/Review", "/Review/Index", "/Review/Interests", "/Review/Availability",
            "/Review/Conflicts", "/Review/Guidelines", "/Review/MyCertificates",
            "/Certificates", "/Dashboard", "/Dashboard/MyConferences"
        ]);

    [Fact]
    public Task DinleyiciEkranlari_SunucuHatasiVermiyor() =>
        SweepAsync("Listener",
        [
            "/Dashboard", "/Dashboard/MyConferences", "/listener-panel",
            "/Certificates", "/Accommodation", "/Payment/My", "/Notification/Index"
        ]);
}
