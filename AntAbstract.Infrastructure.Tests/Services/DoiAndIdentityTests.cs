using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class DoiAndIdentityTests
{
    // ── DOI ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submission_DoiUrl_CanBeSetAndPersisted()
    {
        var options = CreateOptions();
        var subId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Submissions.Add(new Submission
            {
                Id = subId,
                TenantId = Guid.NewGuid(),
                ConferenceId = Guid.NewGuid(),
                Title = "Paper",
                Abstract = "Abs",
                Keywords = "k",
                AuthorId = "a1",
                Status = SubmissionStatus.Accepted
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(options))
        {
            var sub = await ctx.Submissions.FirstAsync(s => s.Id == subId);
            sub.DoiUrl = "https://doi.org/10.1234/test.2026";
            await ctx.SaveChangesAsync();
        }

        await using var verify = CreateContext(options);
        var result = await verify.Submissions.FirstAsync(s => s.Id == subId);
        Assert.Equal("https://doi.org/10.1234/test.2026", result.DoiUrl);
    }

    [Fact]
    public void DoiUrl_AcceptsNull()
    {
        var sub = new Submission
        {
            TenantId = Guid.NewGuid(),
            ConferenceId = Guid.NewGuid(),
            Title = "T",
            Abstract = "A",
            Keywords = "K",
            AuthorId = "a"
        };
        Assert.Null(sub.DoiUrl);
    }

    [Theory]
    [InlineData("https://doi.org/10.1234/test", true)]
    [InlineData("https://doi.org/10.5555/abc.2026.001", true)]
    [InlineData("not-a-url", false)]
    [InlineData("", false)]
    public void DoiUrl_ValidationLogic(string url, bool expectedValid)
    {
        var isValid = !string.IsNullOrWhiteSpace(url) &&
                      Uri.TryCreate(url, UriKind.Absolute, out _);
        Assert.Equal(expectedValid, isValid);
    }

    // ── ORCID ────────────────────────────────────────────────────────────────

    [Fact]
    public void AppUser_OrcidId_DefaultsToNull()
    {
        var user = new AppUser { Email = "test@test.com", UserName = "test" };
        Assert.Null(user.OrcidId);
    }

    [Fact]
    public async Task AppUser_OrcidId_CanBePersisted()
    {
        var options = CreateOptions();
        var userId = "orcid-user-1";

        await using (var ctx = CreateContext(options))
        {
            ctx.Users.Add(new AppUser
            {
                Id = userId,
                Email = "orcid@test.com",
                UserName = "orcid@test.com",
                OrcidId = "0000-0002-1234-5678",
                NormalizedEmail = "ORCID@TEST.COM",
                NormalizedUserName = "ORCID@TEST.COM"
            });
            await ctx.SaveChangesAsync();
        }

        await using var verify = CreateContext(options);
        var user = await verify.Users.FirstAsync(u => u.Id == userId);
        Assert.Equal("0000-0002-1234-5678", user.OrcidId);
    }

    // ── Password / Identity ──────────────────────────────────────────────────

    [Fact]
    public void AppUser_RequiredFields_AreSet()
    {
        var user = new AppUser
        {
            Id = "u1",
            Email = "user@test.com",
            UserName = "user@test.com",
            FirstName = "Test",
            LastName = "User"
        };

        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.Equal("user@test.com", user.Email);
    }

    [Fact]
    public void Registration_QrToken_IsUrlSafe()
    {
        for (int i = 0; i < 50; i++)
        {
            var reg = new Registration
            {
                AppUserId = "u",
                ConferenceId = Guid.NewGuid(),
                RegistrationTypeId = Guid.NewGuid(),
                Amount = 0
            };
            Assert.DoesNotContain("/", reg.QrToken);
            Assert.DoesNotContain("+", reg.QrToken);
            Assert.DoesNotContain("=", reg.QrToken);
            Assert.Equal(22, reg.QrToken.Length);
        }
    }

    // ── PlagiarismReport ─────────────────────────────────────────────────────

    [Fact]
    public async Task PlagiarismReport_FullLifecycle()
    {
        var options = CreateOptions();
        var subId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Submissions.Add(new Submission
            {
                Id = subId,
                TenantId = Guid.NewGuid(),
                ConferenceId = Guid.NewGuid(),
                Title = "Paper",
                Abstract = "A",
                Keywords = "K",
                AuthorId = "a1",
                Status = SubmissionStatus.New
            });

            ctx.PlagiarismReports.Add(new PlagiarismReport
            {
                Id = reportId,
                SubmissionId = subId,
                TenantId = Guid.NewGuid(),
                Status = PlagiarismStatus.Pending
            });
            await ctx.SaveChangesAsync();

            var report = await ctx.PlagiarismReports.FirstAsync(r => r.Id == reportId);
            report.Status = PlagiarismStatus.Processing;
            report.ExternalId = "ext-abc";
            await ctx.SaveChangesAsync();

            report.Status = PlagiarismStatus.Completed;
            report.SimilarityScore = 12;
            report.CompletedAt = DateTime.UtcNow;
            report.ReportUrl = "https://turnitin.com/report/123";
            await ctx.SaveChangesAsync();
        }

        await using var verify = CreateContext(options);
        var r = await verify.PlagiarismReports.FirstAsync(x => x.Id == reportId);
        Assert.Equal(PlagiarismStatus.Completed, r.Status);
        Assert.Equal(12, r.SimilarityScore);
        Assert.NotNull(r.CompletedAt);
        Assert.Equal("https://turnitin.com/report/123", r.ReportUrl);
    }

    // ── Conference IsBlindReview ──────────────────────────────────────────────

    [Fact]
    public async Task Conference_IsBlindReview_PersistsCorrectly()
    {
        var options = CreateOptions();
        var confId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Conferences.Add(new Conference
            {
                Id = confId,
                TenantId = Guid.NewGuid(),
                Title = "Blind Test",
                Slug = "blind-test",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1),
                IsBlindReview = false
            });
            await ctx.SaveChangesAsync();
        }

        await using var verify = CreateContext(options);
        var conf = await verify.Conferences.FirstAsync(c => c.Id == confId);
        Assert.False(conf.IsBlindReview);
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"doi-identity-{Guid.NewGuid()}")
            .Options;

    private static AppDbContext CreateContext(DbContextOptions<AppDbContext> options) =>
        new(options, new TenantContext { IsGlobalContext = true });
}
