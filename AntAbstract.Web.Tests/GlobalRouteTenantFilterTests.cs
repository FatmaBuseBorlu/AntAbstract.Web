using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Aynı ekran slug'lı ve slug'sız adreste aynı veriyi göstermeli.
///
/// Slug yoksa TenantContext.Current null kalıyor ve global erişim yalnızca
/// SuperAdmin için açılıyor; kiracı sorgu filtresi de normal kullanıcı için
/// her satırı eliyor. Bu yüzden slug'sız adreslerde listeler sessizce boş
/// dönebiliyor — hata verilmediği için fark edilmesi zor.
///
/// Karşılaştırma kasıtlı: "boş liste" tek başına kanıt değil, ama slug'lı
/// adres doluyken slug'sızın boş olması kesin olarak bu hatadır.
/// </summary>
public sealed class GlobalRouteTenantFilterTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _referee;
    private readonly ITestOutputHelper _output;

    private const string RefereeId = "global-rota-hakem";
    private const string AuthorId = "global-rota-yazar";
    private const string SubmissionTitle = "Global Rota Bildirisi";
    private const string TenantSlug = "global-rota-kurum";

    private static readonly Guid TenantId = new("99990000-1111-2222-3333-444455556666");
    private static readonly Guid ConferenceId = new("99990000-1111-2222-3333-444455556667");
    private static readonly Guid SubmissionId = new("99990000-1111-2222-3333-444455556668");
    private static readonly Guid CertificateId = new("99990000-1111-2222-3333-444455556669");

    public GlobalRouteTenantFilterTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;

        _referee = factory.CreateClient(new() { AllowAutoRedirect = false });
        _referee.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Referee");
        _referee.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, RefereeId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        foreach (var (id, ad) in new[] { (RefereeId, "Hakem"), (AuthorId, "Yazar") })
        {
            db.Users.Add(new AppUser
            {
                Id = id,
                UserName = $"{id}@test.local",
                NormalizedUserName = $"{id}@TEST.LOCAL".ToUpperInvariant(),
                Email = $"{id}@test.local",
                NormalizedEmail = $"{id}@TEST.LOCAL".ToUpperInvariant(),
                FirstName = ad,
                LastName = "Test",
                SecurityStamp = Guid.NewGuid().ToString()
            });
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Global Rota Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Global Rota Kongresi 2026",
            Slug = "global-rota-kongre",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32),
            City = "İzmir",
            Country = "Türkiye"
        });

        db.Submissions.Add(new Submission
        {
            Id = SubmissionId,
            TenantId = TenantId,
            ConferenceId = ConferenceId,
            AuthorId = AuthorId,
            Title = SubmissionTitle,
            Abstract = "Özet",
            CreatedAt = DateTime.UtcNow
        });

        db.ReviewAssignments.Add(new ReviewAssignment
        {
            SubmissionId = SubmissionId,
            ReviewerId = RefereeId,
            AssignedDate = DateTime.UtcNow
        });

        db.Certificates.Add(new Certificate
        {
            Id = CertificateId,
            ConferenceId = ConferenceId,
            UserId = RefereeId,
            Type = CertificateType.Reviewer,
            EligibleAt = DateTime.UtcNow,
            GeneratedAt = DateTime.UtcNow,
            FileName = "global-rota-sertifika.pdf",
            FilePath = "wwwroot/certificates/global-rota-sertifika.pdf"
        });

        db.SaveChanges();
    }

    private static async Task<string> BodyAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);

        if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
        {
            response = await client.GetAsync(response.Headers.Location.ToString());
        }

        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Hakem_GorevListesi_SlugsuzAdresteDeDolu()
    {
        var withSlug = await BodyAsync(_referee, $"/{TenantSlug}/Review");
        var withoutSlug = await BodyAsync(_referee, "/Review");

        var slugHas = withSlug.Contains(SubmissionTitle, StringComparison.Ordinal);
        var globalHas = withoutSlug.Contains(SubmissionTitle, StringComparison.Ordinal);

        _output.WriteLine($"slug'lı  : {slugHas}");
        _output.WriteLine($"slug'sız : {globalHas}");

        Assert.True(slugHas, "Slug'lı adreste görev görünmüyor — kurulum hatalı.");
        Assert.True(globalHas, "Slug'sız /Review adresinde hakemin görevi kayboldu.");
    }

    [Fact]
    public async Task Sertifikalar_SlugsuzAdresteDeDolu()
    {
        var withSlug = await BodyAsync(_referee, $"/{TenantSlug}/Certificates");
        var withoutSlug = await BodyAsync(_referee, "/Certificates");

        // Sertifika satırı gerçekten basıldıysa indirme bağlantısı çıkar;
        // kongre adı sayfa çerçevesinde de geçtiği için işaret olarak zayıf.
        var marker = CertificateId.ToString();

        var slugHas = withSlug.Contains(marker, StringComparison.Ordinal);
        var globalHas = withoutSlug.Contains(marker, StringComparison.Ordinal);

        _output.WriteLine($"sertifika slug'lı  : {slugHas}");
        _output.WriteLine($"sertifika slug'sız : {globalHas}");

        Assert.True(slugHas, "Slug'lı adreste sertifika görünmüyor — kurulum hatalı.");
        Assert.True(globalHas, "Slug'sız /Certificates adresinde sertifika kayboldu.");
    }
}
