using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Infrastructure.Services
{
    public sealed class BroadcastWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BroadcastWorker> _logger;

        public BroadcastWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<BroadcastWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingBroadcastsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Zamanlanmış broadcast işlemi sırasında hata.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessPendingBroadcastsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueue>();

            var due = await context.ScheduledBroadcasts
                .Where(b => b.Status == BroadcastStatus.Pending && b.ScheduledAt <= DateTime.UtcNow)
                .Include(b => b.Conference)
                .ToListAsync(ct);

            foreach (var broadcast in due)
            {
                try
                {
                    var emails = await GetEmailsAsync(context, broadcast);

                    foreach (var email in emails)
                    {
                        emailQueue.Enqueue(new EmailQueueItem(email, broadcast.Subject, broadcast.HtmlBody));
                    }

                    broadcast.Status = BroadcastStatus.Sent;
                    broadcast.SentAt = DateTime.UtcNow;
                    broadcast.RecipientCount = emails.Count;

                    _logger.LogInformation(
                        "Zamanlanmış broadcast gönderildi: Id={Id}, Alıcı={Count}",
                        broadcast.Id, emails.Count);
                }
                catch (Exception ex)
                {
                    broadcast.Status = BroadcastStatus.Failed;
                    broadcast.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Broadcast Id={Id} gönderilemedi.", broadcast.Id);
                }
            }

            if (due.Count > 0)
                await context.SaveChangesAsync(ct);
        }

        private static async Task<List<string>> GetEmailsAsync(AppDbContext context, ScheduledBroadcast broadcast)
        {
            var conferenceId = broadcast.ConferenceId;

            IQueryable<string?> q = broadcast.TargetGroup switch
            {
                "accepted" => context.Submissions.AsNoTracking()
                    .Where(s => s.ConferenceId == conferenceId &&
                        (s.Status == SubmissionStatus.Accepted || s.Status == SubmissionStatus.Presented))
                    .Select(s => s.Author!.Email),

                "reviewers" => context.ReviewAssignments.AsNoTracking()
                    .Where(a => a.Submission.ConferenceId == conferenceId)
                    .Select(a => a.Reviewer!.Email),

                "registered" => context.Registrations.AsNoTracking()
                    .Where(r => r.ConferenceId == conferenceId)
                    .Select(r => r.AppUser!.Email),

                "paid" => context.Payments.AsNoTracking()
                    .Where(p => p.ConferenceId == conferenceId && p.Status == PaymentStatus.Completed)
                    .Select(p => p.AppUser!.Email),

                "revision" => context.Submissions.AsNoTracking()
                    .Where(s => s.ConferenceId == conferenceId && s.Status == SubmissionStatus.RevisionRequired)
                    .Select(s => s.Author!.Email),

                "unpaid" => context.Registrations.AsNoTracking()
                    .Where(r => r.ConferenceId == conferenceId && !r.IsPaid &&
                        r.Status != RegistrationStatus.Cancelled)
                    .Select(r => r.AppUser!.Email),

                _ => context.Submissions.AsNoTracking()
                    .Where(s => s.ConferenceId == conferenceId)
                    .Select(s => s.Author!.Email),
            };

            return await q
                .Where(e => e != null && e.Contains("@"))
                .Distinct()
                .Cast<string>()
                .ToListAsync();
        }
    }
}
