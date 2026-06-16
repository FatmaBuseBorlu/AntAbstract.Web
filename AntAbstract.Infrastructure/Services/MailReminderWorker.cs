using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using Microsoft.EntityFrameworkCore;
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

        // Worker tetiklenme sıklığı
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MailReminderWorker> _logger;

        public MailReminderWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<MailReminderWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
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

                await Task.Delay(Interval, stoppingToken);
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
            var warningStart = now;
            var warningEnd = now.AddDays(DeadlineWarningDays);

            // Deadline penceresi içindeki aktif kongreler
            var conferences = await db.Conferences
                .AsNoTracking()
                .Where(c =>
                    c.IsSubmissionOpen &&
                    c.AbstractSubmissionDeadline.HasValue &&
                    c.AbstractSubmissionDeadline.Value >= warningStart &&
                    c.AbstractSubmissionDeadline.Value <= warningEnd)
                .Include(c => c.Registrations)
                    .ThenInclude(r => r.AppUser)
                .ToListAsync(ct);

            foreach (var conf in conferences)
            {
                var deadline = conf.AbstractSubmissionDeadline!.Value;
                var daysLeft = (int)Math.Ceiling((deadline - now).TotalDays);

                // Bu kongreye kayıtlı, henüz özet göndermemiş kullanıcılar
                var submittedUserIds = await db.Submissions
                    .AsNoTracking()
                    .Where(s => s.ConferenceId == conf.Id)
                    .Select(s => s.AuthorId)
                    .ToListAsync(ct);

                foreach (var reg in conf.Registrations)
                {
                    var user = reg.AppUser;
                    if (user?.Email == null) continue;
                    if (submittedUserIds.Contains(user.Id)) continue;

                    const string templateKey = "deadline_reminder";

                    if (await WasRecentlySentAsync(db, user.Email, templateKey, ct)) continue;

                    var placeholders = new Dictionary<string, string>
                    {
                        ["{FullName}"]        = $"{user.FirstName} {user.LastName}".Trim(),
                        ["{ConferenceName}"]  = conf.Title,
                        ["{Deadline}"]        = deadline.ToString("dd.MM.yyyy HH:mm"),
                        ["{DaysLeft}"]        = daysLeft.ToString(),
                    };

                    try
                    {
                        await email.SendTemplatedAsync(user.Email, templateKey, placeholders);
                        _logger.LogInformation(
                            "Deadline hatırlatma gönderildi: {Email} ({Conf}, {Days} gün kaldı)",
                            user.Email, conf.Title, daysLeft);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Deadline hatırlatma gönderilemedi: {Email}", user.Email);
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

            foreach (var payment in pendingPayments)
            {
                var user = payment.AppUser;
                if (user?.Email == null) continue;

                const string templateKey = "payment_pending_reminder";

                if (await WasRecentlySentAsync(db, user.Email, templateKey, ct)) continue;

                var placeholders = new Dictionary<string, string>
                {
                    ["{FullName}"]        = $"{user.FirstName} {user.LastName}".Trim(),
                    ["{ConferenceName}"]  = payment.Conference?.Title ?? "",
                    ["{Amount}"]          = $"{payment.Amount:N2} {payment.Currency}",
                    ["{PaymentDate}"]     = payment.PaymentDate.ToString("dd.MM.yyyy"),
                };

                try
                {
                    await email.SendTemplatedAsync(user.Email, templateKey, placeholders);
                    _logger.LogInformation(
                        "Ödeme hatırlatma gönderildi: {Email} (PaymentId={Id})",
                        user.Email, payment.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Ödeme hatırlatma gönderilemedi: {Email}", user.Email);
                }
            }
        }

        // ── Deduplication ────────────────────────────────────────────────────

        private static async Task<bool> WasRecentlySentAsync(
            AppDbContext db, string toEmail, string templateKey, CancellationToken ct)
        {
            var since = DateTime.UtcNow.AddDays(-DedupWindowDays);
            return await db.EmailLogs
                .AsNoTracking()
                .AnyAsync(l =>
                    l.ToEmail == toEmail &&
                    l.TemplateKey == templateKey &&
                    l.SentAt >= since &&
                    l.Status == "sent",
                    ct);
        }
    }
}
