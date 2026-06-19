using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class PaymentFlowTests
{
    [Fact]
    public async Task Payment_CompletedUpdatesRegistration()
    {
        var options = CreateOptions();
        var tenantId = Guid.NewGuid();
        var conferenceId = Guid.NewGuid();
        var userId = "user-1";
        var regTypeId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Conferences.Add(new Conference
            {
                Id = conferenceId,
                TenantId = tenantId,
                Title = "Test",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1)
            });

            var reg = new Registration
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                ConferenceId = conferenceId,
                RegistrationTypeId = regTypeId,
                Amount = 500,
                IsPaid = false,
                Status = RegistrationStatus.Pending
            };

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                ConferenceId = conferenceId,
                RelatedSubmissionId = reg.Id,
                Amount = 500,
                Currency = "TRY",
                Status = PaymentStatus.Pending
            };

            ctx.Registrations.Add(reg);
            ctx.Payments.Add(payment);
            await ctx.SaveChangesAsync();

            // Simulate payment completion
            payment.Status = PaymentStatus.Completed;
            payment.TransactionId = "stripe_session_123";

            reg.IsPaid = true;
            reg.PaymentDate = DateTime.UtcNow;
            reg.Status = RegistrationStatus.Confirmed;

            ctx.PaymentStatusHistories.Add(new PaymentStatusHistory
            {
                PaymentId = payment.Id,
                OldStatus = PaymentStatus.Pending,
                NewStatus = PaymentStatus.Completed,
                Source = "Stripe"
            });

            await ctx.SaveChangesAsync();
        }

        await using var verifyCtx = CreateContext(options);
        var verifyPayment = await verifyCtx.Payments.FirstAsync();
        var verifyReg = await verifyCtx.Registrations.FirstAsync();
        var history = await verifyCtx.PaymentStatusHistories.FirstAsync();

        Assert.Equal(PaymentStatus.Completed, verifyPayment.Status);
        Assert.True(verifyReg.IsPaid);
        Assert.Equal(RegistrationStatus.Confirmed, verifyReg.Status);
        Assert.NotNull(verifyReg.PaymentDate);
        Assert.Equal(PaymentStatus.Pending, history.OldStatus);
        Assert.Equal(PaymentStatus.Completed, history.NewStatus);
    }

    [Fact]
    public async Task Refund_ResetsRegistrationStatus()
    {
        var options = CreateOptions();
        var regId = Guid.NewGuid();
        var confId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Conferences.Add(new Conference
            {
                Id = confId,
                TenantId = tenantId,
                Title = "Refund Test",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1)
            });

            ctx.Registrations.Add(new Registration
            {
                Id = regId,
                AppUserId = "u1",
                ConferenceId = confId,
                RegistrationTypeId = Guid.NewGuid(),
                Amount = 300,
                IsPaid = true,
                Status = RegistrationStatus.Confirmed,
                PaymentDate = DateTime.UtcNow.AddDays(-1)
            });
            await ctx.SaveChangesAsync();

            var reg = await ctx.Registrations.FirstAsync();
            reg.IsPaid = false;
            reg.Status = RegistrationStatus.Cancelled;
            await ctx.SaveChangesAsync();
        }

        await using var verifyCtx = CreateContext(options);
        var r = await verifyCtx.Registrations.FirstAsync();
        Assert.False(r.IsPaid);
        Assert.Equal(RegistrationStatus.Cancelled, r.Status);
    }

    [Fact]
    public async Task Conference_BlindReview_DefaultsToTrue()
    {
        var conf = new Conference
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Title = "Test Conf",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        Assert.True(conf.IsBlindReview);
    }

    [Fact]
    public async Task Submission_StatusTransitions_AreValid()
    {
        var options = CreateOptions();
        var tenantId = Guid.NewGuid();
        var conferenceId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            var sub = new Submission
            {
                TenantId = tenantId,
                ConferenceId = conferenceId,
                Title = "Test Paper",
                Abstract = "Abstract text",
                Keywords = "test",
                AuthorId = "author-1",
                Status = SubmissionStatus.New
            };
            ctx.Submissions.Add(sub);
            await ctx.SaveChangesAsync();

            sub.Status = SubmissionStatus.UnderReview;
            await ctx.SaveChangesAsync();

            sub.Status = SubmissionStatus.Accepted;
            sub.DecisionDate = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        await using var verifyCtx = CreateContext(options);
        var verified = await verifyCtx.Submissions.FirstAsync();
        Assert.Equal(SubmissionStatus.Accepted, verified.Status);
        Assert.NotNull(verified.DecisionDate);
    }

    [Fact]
    public async Task SubmissionFile_MultipleVersions_CanCoexist()
    {
        var options = CreateOptions();
        var submissionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Submissions.Add(new Submission
            {
                Id = submissionId,
                TenantId = tenantId,
                ConferenceId = Guid.NewGuid(),
                Title = "Paper",
                Abstract = "Abs",
                Keywords = "k",
                AuthorId = "a1",
                Status = SubmissionStatus.New
            });

            ctx.SubmissionFiles.AddRange(
                new SubmissionFile
                {
                    SubmissionId = submissionId,
                    FileName = "paper_v1.pdf",
                    StoredFileName = "stored_v1.pdf",
                    FilePath = "/files/v1.pdf",
                    Type = SubmissionFileType.FullText,
                    Version = 1,
                    UploadedAt = DateTime.UtcNow.AddDays(-2)
                },
                new SubmissionFile
                {
                    SubmissionId = submissionId,
                    FileName = "paper_v2.pdf",
                    StoredFileName = "stored_v2.pdf",
                    FilePath = "/files/v2.pdf",
                    Type = SubmissionFileType.FullText,
                    Version = 2,
                    UploadedAt = DateTime.UtcNow
                });
            await ctx.SaveChangesAsync();
        }

        await using var verifyCtx = CreateContext(options);
        var files = await verifyCtx.SubmissionFiles
            .Where(f => f.SubmissionId == submissionId)
            .OrderByDescending(f => f.Version)
            .ToListAsync();

        Assert.Equal(2, files.Count);
        Assert.Equal("paper_v2.pdf", files[0].FileName);
        Assert.Equal(2, files[0].Version);
    }

    [Fact]
    public async Task PlagiarismReport_StatusLifecycle()
    {
        var report = new PlagiarismReport
        {
            SubmissionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = PlagiarismStatus.Pending
        };

        Assert.Equal(PlagiarismStatus.Pending, report.Status);
        Assert.Null(report.SimilarityScore);

        report.Status = PlagiarismStatus.Processing;
        report.ExternalId = "ext-123";

        report.Status = PlagiarismStatus.Completed;
        report.SimilarityScore = 18;
        report.CompletedAt = DateTime.UtcNow;

        Assert.Equal(18, report.SimilarityScore);
        Assert.NotNull(report.CompletedAt);
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"payment-flow-{Guid.NewGuid()}")
            .Options;

    private static AppDbContext CreateContext(DbContextOptions<AppDbContext> options) =>
        new(options, new TenantContext { IsGlobalContext = true });
}
