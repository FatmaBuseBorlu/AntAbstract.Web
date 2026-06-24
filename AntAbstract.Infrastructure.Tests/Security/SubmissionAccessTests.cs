using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Security;

/// <summary>
/// Yazar ve admin erişim izolasyonu testleri.
///
/// Kurallar:
///  1. Yazar yalnızca kendi tenant'ındaki bildirileri görebilir (query filter).
///  2. Yazar yalnızca AuthorId == userId olan bildirileri düzenleyebilir;
///     ortak yazar (SubmissionAuthors) olanlar da erişebilir.
///  3. Admin yalnızca kendi tenant'ındaki bildirileri görebilir.
///  4. SuperAdmin (IsGlobalContext=true) tüm bildirileri görebilir.
/// </summary>
public sealed class SubmissionAccessTests
{
    // ── Kural 1: Tenant izolasyonu ───────────────────────────────────────────

    [Fact]
    public async Task Author_CannotSee_SubmissionsFromAnotherTenant()
    {
        var (tenantA, tenantB, conferenceA, conferenceB, options) = await SeedTwoTenantsAsync();

        // Tenant A bağlamında sorgulayan bir yazar
        await using var ctx = new AppDbContext(options, new TenantContext { Current = tenantA });

        var submissions = await ctx.Submissions.ToListAsync();

        Assert.All(submissions, s => Assert.Equal(tenantA.Id, s.TenantId));
        Assert.DoesNotContain(submissions, s => s.TenantId == tenantB.Id);
    }

    // ── Kural 2a: AuthorId eşleşmesi ────────────────────────────────────────

    [Fact]
    public async Task Author_CanAccess_OwnSubmission_ByAuthorId()
    {
        var options = CreateOptions();
        var tenant = CreateTenant("t");
        var conference = CreateConference(tenant.Id);
        var ownerSubmission = CreateSubmission(tenant.Id, "author-1", conference);
        var otherSubmission = CreateSubmission(tenant.Id, "author-2", conference);

        await using (var seed = new AppDbContext(options, new TenantContext()))
        {
            seed.AddRange(conference, ownerSubmission, otherSubmission);
            await seed.SaveChangesAsync();
        }

        await using var ctx = new AppDbContext(options, new TenantContext { Current = tenant });
        var all = await ctx.Submissions.ToListAsync();

        // Tenant filter hem kaydı görür, AuthorId kontrolü controller'da yapılır.
        // Burada controller mantığını simüle ediyoruz:
        var visibleToAuthor1 = all.Where(s =>
            s.AuthorId == "author-1" ||
            (s.SubmissionAuthors?.Any(a => a.AppUserId == "author-1") == true)).ToList();

        Assert.Single(visibleToAuthor1);
        Assert.Equal("author-1", visibleToAuthor1[0].AuthorId);
    }

    // ── Kural 2b: Ortak yazar erişimi ────────────────────────────────────────

    [Fact]
    public async Task CoAuthor_CanAccess_Submission_TheyAreListedOn()
    {
        var options = CreateOptions();
        var tenant = CreateTenant("t");
        var conference = CreateConference(tenant.Id);
        var submission = CreateSubmission(tenant.Id, "author-1", conference);
        var coAuthor = new SubmissionAuthor
        {
            SubmissionId = submission.Id,
            Submission = submission,
            FirstName = "Co",
            LastName = "Author",
            AppUserId = "author-2"
        };

        await using (var seed = new AppDbContext(options, new TenantContext()))
        {
            seed.AddRange(conference, submission, coAuthor);
            await seed.SaveChangesAsync();
        }

        await using var ctx = new AppDbContext(
            options,
            new TenantContext { Current = tenant });
        var all = await ctx.Submissions
            .Include(s => s.SubmissionAuthors)
            .ToListAsync();

        var visibleToAuthor2 = all.Where(s =>
            s.AuthorId == "author-2" ||
            (s.SubmissionAuthors?.Any(a => a.AppUserId == "author-2") == true)).ToList();

        Assert.Single(visibleToAuthor2);
    }

    // ── Kural 3: Admin başka tenant'ı göremez ────────────────────────────────

    [Fact]
    public async Task Admin_CannotSee_SubmissionsFromAnotherTenant()
    {
        var (tenantA, tenantB, conferenceA, conferenceB, options) = await SeedTwoTenantsAsync();

        // Admin-A yalnızca Tenant A bağlamında çalışır
        await using var ctx = new AppDbContext(options, new TenantContext { Current = tenantA });

        var submissions = await ctx.Submissions.ToListAsync();

        Assert.DoesNotContain(submissions, s => s.TenantId == tenantB.Id);
    }

    // ── Kural 4: SuperAdmin tüm bildirileri görür ─────────────────────────────

    [Fact]
    public async Task SuperAdmin_CanSee_AllSubmissionsAcrossTenants()
    {
        var (tenantA, tenantB, _, _, options) = await SeedTwoTenantsAsync();

        await using var ctx = new AppDbContext(
            options,
            new TenantContext { IsGlobalContext = true });

        var submissions = await ctx.Submissions.ToListAsync();

        Assert.Contains(submissions, s => s.TenantId == tenantA.Id);
        Assert.Contains(submissions, s => s.TenantId == tenantB.Id);
        Assert.Equal(2, submissions.Count);
    }

    // ── Yardımcı metodlar ────────────────────────────────────────────────────

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"submission-access-{Guid.NewGuid()}")
            .Options;

    private static Tenant CreateTenant(string slug) =>
        new() { Id = Guid.NewGuid(), Name = slug, Slug = slug };

    private static Conference CreateConference(Guid tenantId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = "Conf",
            Slug = Guid.NewGuid().ToString("N")[..12],
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };

    private static Submission CreateSubmission(
        Guid tenantId, string authorId, Conference conference) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AuthorId = authorId,
            Title = $"Submission by {authorId}",
            Abstract = "Abstract",
            Keywords = "test",
            ConferenceId = conference.Id,
            Conference = conference
        };

    private static async Task<(Tenant, Tenant, Conference, Conference, DbContextOptions<AppDbContext>)>
        SeedTwoTenantsAsync()
    {
        var options = CreateOptions();
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var conferenceA = CreateConference(tenantA.Id);
        var conferenceB = CreateConference(tenantB.Id);

        await using var seed = new AppDbContext(options, new TenantContext());
        seed.AddRange(
            conferenceA,
            conferenceB,
            CreateSubmission(tenantA.Id, "author-a", conferenceA),
            CreateSubmission(tenantB.Id, "author-b", conferenceB));
        await seed.SaveChangesAsync();

        return (tenantA, tenantB, conferenceA, conferenceB, options);
    }
}
