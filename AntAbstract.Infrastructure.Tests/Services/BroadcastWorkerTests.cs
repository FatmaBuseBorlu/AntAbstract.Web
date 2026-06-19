using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class BroadcastWorkerTests
{
    [Fact]
    public async Task ProcessesPendingBroadcast_AndMarksSent()
    {
        var (services, options) = BuildServices();
        var tenantId = Guid.NewGuid();
        var conferenceId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Conferences.Add(new Conference
            {
                Id = conferenceId,
                TenantId = tenantId,
                Title = "Test Conf",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1)
            });
            ctx.Registrations.Add(new Registration
            {
                ConferenceId = conferenceId,
                AppUserId = "user1",
                RegistrationTypeId = Guid.NewGuid(),
                Amount = 100,
                AppUser = new AppUser
                {
                    Id = "user1",
                    Email = "user1@test.com",
                    UserName = "user1@test.com"
                }
            });
            ctx.ScheduledBroadcasts.Add(new ScheduledBroadcast
            {
                ConferenceId = conferenceId,
                TenantId = tenantId,
                TargetGroup = "registered",
                Subject = "Test Subject",
                HtmlBody = "<p>Hello</p>",
                ScheduledAt = DateTime.UtcNow.AddMinutes(-5),
                Status = BroadcastStatus.Pending
            });
            await ctx.SaveChangesAsync();
        }

        var worker = new BroadcastWorker(services, NullLogger<BroadcastWorker>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Run one cycle
        await RunOneWorkerCycle(worker, cts.Token);

        await using var verifyCtx = CreateContext(options);
        var broadcast = await verifyCtx.ScheduledBroadcasts.FirstAsync();
        Assert.Equal(BroadcastStatus.Sent, broadcast.Status);
        Assert.NotNull(broadcast.SentAt);
        Assert.Equal(1, broadcast.RecipientCount);
    }

    [Fact]
    public async Task SkipsFutureBroadcasts()
    {
        var (services, options) = BuildServices();
        var tenantId = Guid.NewGuid();
        var conferenceId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Conferences.Add(new Conference
            {
                Id = conferenceId,
                TenantId = tenantId,
                Title = "Future Conf",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1)
            });
            ctx.ScheduledBroadcasts.Add(new ScheduledBroadcast
            {
                ConferenceId = conferenceId,
                TenantId = tenantId,
                TargetGroup = "all",
                Subject = "Future",
                HtmlBody = "<p>Future</p>",
                ScheduledAt = DateTime.UtcNow.AddHours(2),
                Status = BroadcastStatus.Pending
            });
            await ctx.SaveChangesAsync();
        }

        var worker = new BroadcastWorker(services, NullLogger<BroadcastWorker>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await RunOneWorkerCycle(worker, cts.Token);

        await using var verifyCtx = CreateContext(options);
        var broadcast = await verifyCtx.ScheduledBroadcasts.FirstAsync();
        Assert.Equal(BroadcastStatus.Pending, broadcast.Status);
    }

    [Fact]
    public async Task AcceptedGroup_OnlyIncludesAcceptedSubmissionAuthors()
    {
        var (services, options) = BuildServices();
        var tenantId = Guid.NewGuid();
        var conferenceId = Guid.NewGuid();

        await using (var ctx = CreateContext(options))
        {
            ctx.Conferences.Add(new Conference
            {
                Id = conferenceId,
                TenantId = tenantId,
                Title = "Conf",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1)
            });

            var acceptedAuthor = new AppUser { Id = "accepted", Email = "accepted@test.com", UserName = "accepted" };
            var rejectedAuthor = new AppUser { Id = "rejected", Email = "rejected@test.com", UserName = "rejected" };

            ctx.Submissions.AddRange(
                new Submission
                {
                    TenantId = tenantId,
                    ConferenceId = conferenceId,
                    Title = "Accepted Paper",
                    Abstract = "abs",
                    Keywords = "k",
                    AuthorId = "accepted",
                    Author = acceptedAuthor,
                    Status = SubmissionStatus.Accepted
                },
                new Submission
                {
                    TenantId = tenantId,
                    ConferenceId = conferenceId,
                    Title = "Rejected Paper",
                    Abstract = "abs",
                    Keywords = "k",
                    AuthorId = "rejected",
                    Author = rejectedAuthor,
                    Status = SubmissionStatus.Rejected
                });

            ctx.ScheduledBroadcasts.Add(new ScheduledBroadcast
            {
                ConferenceId = conferenceId,
                TenantId = tenantId,
                TargetGroup = "accepted",
                Subject = "Congrats",
                HtmlBody = "<p>Accepted</p>",
                ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
                Status = BroadcastStatus.Pending
            });
            await ctx.SaveChangesAsync();
        }

        var emailQueue = services.CreateScope().ServiceProvider.GetRequiredService<IEmailQueue>() as TestEmailQueue;
        var worker = new BroadcastWorker(services, NullLogger<BroadcastWorker>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await RunOneWorkerCycle(worker, cts.Token);

        await using var verifyCtx = CreateContext(options);
        var broadcast = await verifyCtx.ScheduledBroadcasts.FirstAsync();
        Assert.Equal(BroadcastStatus.Sent, broadcast.Status);
        Assert.Equal(1, broadcast.RecipientCount);
    }

    private static async Task RunOneWorkerCycle(BroadcastWorker worker, CancellationToken ct)
    {
        var method = typeof(BroadcastWorker)
            .GetMethod("ProcessPendingBroadcastsAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(worker, new object[] { ct })!;
    }

    private static (IServiceScopeFactory, DbContextOptions<AppDbContext>) BuildServices()
    {
        var dbName = $"broadcast-test-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddScoped(sp =>
            new AppDbContext(sp.GetRequiredService<DbContextOptions<AppDbContext>>(),
                new TenantContext { IsGlobalContext = true }));
        services.AddSingleton<IEmailQueue, TestEmailQueue>();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IServiceScopeFactory>(), options);
    }

    private static AppDbContext CreateContext(DbContextOptions<AppDbContext> options) =>
        new(options, new TenantContext { IsGlobalContext = true });

    private sealed class TestEmailQueue : IEmailQueue
    {
        private readonly List<EmailQueueItem> _items = new();
        public IReadOnlyList<EmailQueueItem> Items => _items;

        public void Enqueue(EmailQueueItem item) => _items.Add(item);

        public Task<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_items.First());
    }
}
