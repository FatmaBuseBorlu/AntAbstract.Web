using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Infrastructure.Services
{
    /// <summary>
    /// Zamanlanmış mail hatırlatıcıları:
    ///   1. Özet gönderim son tarihi yaklaşan aktif kongreler için kayıtlı kullanıcılara hatırlatma.
    ///   2. 3 günden eski Pending ödemeler için kullanıcılara hatırlatma.
    ///
    /// Deduplication: aynı (alıcı, şablon, kongre) için son 5 gün içinde kayıt varsa tekrar gönderilmez.
    /// </summary>
    public sealed class MailReminderWorker : BackgroundService
    {
        // Kaç gün kala deadline uyarısı gönderilsin
        private const int DeadlineWarningDays = 3;

        // Kaç günden eski pending ödemelere hatırlatma yapılsın
        private const int PaymentPendingDays = 3;

        // Aynı kişiye aynı şablon ne kadar süre içinde tekrar gönderilmesin (dedup penceresi)
        private const int DedupWindowDays = 5;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MailReminderWorker> _logger;
        private readonly TimeSpan _interval;

        public MailReminderWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<MailReminderWorker> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            var hours = configuration.GetValue<double>("MailReminder:IntervalHours", 6);
            _interval = TimeSpan.FromHours(hours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Mail hatırlatıcı işçisi başladı.");

            // İlk çalışmayı biraz ertele (uygulama ayağa kalkarken DB hazır değil olabilir)
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mail hatırlatıcı döngüsünde hata oluştu.");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("Mail hatırlatıcı işçisi durdu.");
        }

        private async Task RunAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            await SendDeadlineRemindersAsync(db, emailService, ct);
            await SendPaymentPendingRemindersAsync(db, emailService, ct);
        }

        // ── 1. Son tarihi yaklaşan kongreler ─────────────────────────────────

        private async Task SendDeadlineRemindersAsync(
            AppDbContext db, IEmailService email, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var warningEnd = now.AddDays(DeadlineWarningDays);

            var conferences = await db.Conferences
                .AsNoTracking()
                .Where(c =>
                    c.IsSubmissionOpen &&
                    c.AbstractSubmissionDeadline.HasValue &&
                    c.AbstractSubmissionDeadline.Value >= now &&
                    c.AbstractSubmissionDeadline.Value <= warningEnd)
                .Include(c => c.Registrations)
                    .ThenInclude(r => r.AppUser)
                .ToListAsync(ct);

            if (conferences.Count == 0) return;

            const string templateKey = "deadline_reminder";
            var confIds = conferences.Select(c => c.Id).ToList();

            // Tüm kongreler için özet gönderen kullanıcı ID'leri — tek sorgu
            var submittedUserIds = await db.Submissions
                .AsNoTracking()
                .Where(s => confIds.Contains(s.ConferenceId))
                .Select(s => new { s.ConferenceId, s.AuthorId })
                .ToListAsync(ct);
            var submittedSet = submittedUserIds
                .Select(x => (x.ConferenceId, x.AuthorId))
                .ToHashSet();

            // Potansiyel alıcı e-postaları — dedup kontrolü için tek toplu sorgu
            var candidateEmails = conferences
                .SelectMany(c => c.Registrations)
                .Select(r => r.AppUser?.Email)
                .Where(e => e != null)
                .Distinct()
                .ToList();

            var recentlySent = await RecentlySentSetAsync(db, candidateEmails!, templateKey, ct);

            foreach (var conf in conferences)
            {
                var deadline = conf.AbstractSubmissionDeadline!.Value;
                var daysLeft = (int)Math.Ceiling((deadline - now).TotalDays);

                foreach (var reg in conf.Registrations)
                {
                    var user = reg.AppUser;
                    if (user?.Email == null) continue;
                    if (submittedSet.Contains((conf.Id, user.Id))) continue;
                    if (recentlySent.Contains(user.Email)) continue;

                    var placeholders = new Dictionary<string, string>
                    {
                        ["{FullName}"] = $"{user.FirstName} {user.LastName}".Trim(),
                        ["{ConferenceName}"] = conf.Title,
                        ["{Deadline}"] = deadline.ToString("dd.MM.yyyy HH:mm"),
                        ["{DaysLeft}"] = daysLeft.ToString(),
                    };

                    try
                    {
                        await email.SendTemplatedAsync(user.Email, templateKey, placeholders);
                        recentlySent.Add(user.Email); // aynı çalışmada tekrar gönderme
                        _logger.LogInformation(
                            "Deadline hatırlatma gönderildi: {Email} ({Conf}, {Days} gün kaldı)",
                            user.Email, conf.Title, daysLeft);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Deadline hatırlatma gönderilemedi: {Email}", user.Email);
                    }
                }
            }
        }

        // ── 2. Bekleyen ödemeler ──────────────────────────────────────────────

        private async Task SendPaymentPendingRemindersAsync(
            AppDbContext db, IEmailService email, CancellationToken ct)
        {
            var cutoff = DateTime.UtcNow.AddDays(-PaymentPendingDays);

            var pendingPayments = await db.Payments
                .AsNoTracking()
                .Include(p => p.AppUser)
                .Include(p => p.Conference)
                .Where(p =>
                    p.Status == PaymentStatus.Pending &&
                    p.PaymentDate <= cutoff)
                .ToListAsync(ct);

            if (pendingPayments.Count == 0) return;

            const string templateKey = "payment_pending_reminder";

            var candidateEmails = pendingPayments
                .Select(p => p.AppUser?.Email)
                .Where(e => e != null)
                .Distinct()
                .ToList();

            var recentlySent = await RecentlySentSetAsync(db, candidateEmails!, templateKey, ct);

            foreach (var payment in pendingPayments)
            {
                var user = payment.AppUser;
                if (user?.Email == null) continue;
                if (recentlySent.Contains(user.Email)) continue;

                var placeholders = new Dictionary<string, string>
                {
                    ["{FullName}"] = $"{user.FirstName} {user.LastName}".Trim(),
                    ["{ConferenceName}"] = payment.Conference?.Title ?? "",
                    ["{Amount}"] = $"{payment.Amount:N2} {payment.Currency}",
                    ["{PaymentDate}"] = payment.PaymentDate.ToString("dd.MM.yyyy"),
                };

                try
                {
                    await email.SendTemplatedAsync(user.Email, templateKey, placeholders);
                    recentlySent.Add(user.Email);
                    _logger.LogInformation(
                        "Ödeme hatırlatma gönderildi: {Email} (PaymentId={Id})",
                        user.Email, payment.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ödeme hatırlatma gönderilemedi: {Email}", user.Email);
                }
            }
        }

        // ── Deduplication — toplu sorgu, N+1 yok ────────────────────────────

        private static async Task<HashSet<string>> RecentlySentSetAsync(
            AppDbContext db,
            List<string> emails,
            string templateKey,
            CancellationToken ct)
        {
            var since = DateTime.UtcNow.AddDays(-DedupWindowDays);
            var sent = await db.EmailLogs
                .AsNoTracking()
                .Where(l =>
                    emails.Contains(l.ToEmail) &&
                    l.TemplateKey == templateKey &&
                    l.SentAt >= since &&
                    l.Status == "sent")
                .Select(l => l.ToEmail)
                .Distinct()
                .ToListAsync(ct);
            return new HashSet<string>(sent, StringComparer.OrdinalIgnoreCase);
        }
    }
}
