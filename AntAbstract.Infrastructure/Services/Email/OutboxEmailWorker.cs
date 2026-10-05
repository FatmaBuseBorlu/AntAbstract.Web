using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Infrastructure.Services.Email
{
    /// <summary>
    /// Giden kutusundaki e-postaları gönderir.
    ///  - Her satırı göndermeden önce kilitler: IIS geri dönüşümünde iki süreç
    ///    kısa süre birlikte çalışabiliyor, aynı e-posta iki kez gitmez.
    ///  - Hata olursa 1 dk, 5 dk, 30 dk, 2 sa sonra tekrar dener; 5. denemeden
    ///    sonra "başarısız" işaretler (Sistem Durumu'ndan yeniden denenebilir).
    ///  - Dakikada en çok Email:MaxPerMinute (varsayılan 30) gönderir: toplu
    ///    duyuruda SMTP sağlayıcısı engellemesin / spam saymasın.
    ///  - Gönderilmiş satırları 90 gün sonra siler.
    /// </summary>
    public sealed class OutboxEmailWorker : BackgroundService
    {
        public const int MaxAttempts = 5;
        private static readonly TimeSpan[] Backoff =
        {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)
        };
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SentRetention = TimeSpan.FromDays(90);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxEmailWorker> _logger;
        private readonly int _maxPerMinute;
        private readonly Queue<DateTime> _recentSends = new();
        private DateTime _lastCleanup = DateTime.MinValue;

        public OutboxEmailWorker(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<OutboxEmailWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _maxPerMinute = int.TryParse(configuration["Email:MaxPerMinute"], out var max) && max > 0 ? max : 30;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Giden kutusu işlenirken hata.");
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        /// <summary>Bir tur: zamanı gelmiş satırları kilitleyip gönderir. Gönderilen sayısını döner.</summary>
        public async Task<int> ProcessBatchAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Kilit ve toplu silme ilişkisel SQL ister (InMemory test sağlayıcısında yok).
            if (!context.Database.IsRelational())
                return 0;

            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var now = DateTime.UtcNow;

            await CleanupAsync(context, now, ct);

            while (_recentSends.Count > 0 && _recentSends.Peek() < now.AddMinutes(-1))
                _recentSends.Dequeue();

            var budget = Math.Min(20, _maxPerMinute - _recentSends.Count);
            if (budget <= 0)
                return 0;

            var candidateIds = await context.EmailOutbox
                .AsNoTracking()
                .Where(m => m.Status == EmailOutboxStatus.Pending &&
                            m.NextAttemptAt <= now &&
                            (m.LockedUntil == null || m.LockedUntil < now))
                .OrderBy(m => m.NextAttemptAt)
                .ThenBy(m => m.Id)
                .Select(m => m.Id)
                .Take(budget)
                .ToListAsync(ct);

            var sent = 0;

            foreach (var id in candidateIds)
            {
                var lockUntil = DateTime.UtcNow.Add(LockDuration);

                // Atomik sahiplenme: başka süreç aynı satırı aldıysa 0 döner.
                var claimed = await context.EmailOutbox
                    .Where(m => m.Id == id &&
                                m.Status == EmailOutboxStatus.Pending &&
                                (m.LockedUntil == null || m.LockedUntil < now))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntil, lockUntil), ct);

                if (claimed == 0)
                    continue;

                var message = await context.EmailOutbox.AsNoTracking().FirstAsync(m => m.Id == id, ct);

                try
                {
                    await emailService.SendAsync(message.ToEmail, message.Subject, message.HtmlBody);

                    await context.EmailOutbox
                        .Where(m => m.Id == id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(m => m.Status, EmailOutboxStatus.Sent)
                            .SetProperty(m => m.SentAt, DateTime.UtcNow)
                            .SetProperty(m => m.Attempts, message.Attempts + 1)
                            .SetProperty(m => m.LockedUntil, (DateTime?)null)
                            .SetProperty(m => m.LastError, (string?)null), ct);

                    _recentSends.Enqueue(DateTime.UtcNow);
                    sent++;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    var attempts = message.Attempts + 1;
                    var failed = attempts >= MaxAttempts;
                    var error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                    var next = DateTime.UtcNow.Add(Backoff[Math.Min(attempts - 1, Backoff.Length - 1)]);

                    await context.EmailOutbox
                        .Where(m => m.Id == id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(m => m.Attempts, attempts)
                            .SetProperty(m => m.Status, failed ? EmailOutboxStatus.Failed : EmailOutboxStatus.Pending)
                            .SetProperty(m => m.NextAttemptAt, next)
                            .SetProperty(m => m.LockedUntil, (DateTime?)null)
                            .SetProperty(m => m.LastError, error), ct);

                    _logger.LogWarning(ex,
                        "E-posta gönderilemedi ({Attempt}/{Max}) → {To}{Final}",
                        attempts, MaxAttempts, message.ToEmail, failed ? " — vazgeçildi" : "");
                }
            }

            return sent;
        }

        private async Task CleanupAsync(AppDbContext context, DateTime now, CancellationToken ct)
        {
            if (now - _lastCleanup < TimeSpan.FromHours(1))
                return;

            _lastCleanup = now;
            var cutoff = now - SentRetention;

            var deleted = await context.EmailOutbox
                .Where(m => m.Status == EmailOutboxStatus.Sent && m.SentAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
                _logger.LogInformation("Giden kutusundan {Count} eski e-posta silindi.", deleted);
        }
    }
}
