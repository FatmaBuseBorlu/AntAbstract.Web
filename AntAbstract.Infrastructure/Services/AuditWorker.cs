using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Infrastructure.Services
{
    /// <summary>
    /// AuditQueue'dan kayıtları okuyup batch olarak veritabanına yazan background service.
    /// Graceful shutdown sırasında kuyruktaki kalan kayıtları tüketmeye çalışır.
    /// </summary>
    public sealed class AuditWorker : BackgroundService
    {
        private readonly AuditQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditWorker> _logger;

        private const int BatchSize = 50;

        public AuditWorker(
            AuditQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<AuditWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<AuditLog>(BatchSize);

            await foreach (var entry in _queue.ReadAllAsync(stoppingToken))
            {
                batch.Add(entry);

                // Kanalda hemen daha fazla kayıt varsa beklemeden al (batch dol)
                while (batch.Count < BatchSize && _queue.TryRead(out var next) && next != null)
                    batch.Add(next);

                await FlushAsync(batch);
                batch.Clear();
            }

            // Graceful shutdown: kalan kayıtları yaz
            while (_queue.TryRead(out var remaining) && remaining != null)
                batch.Add(remaining);

            if (batch.Count > 0)
                await FlushAsync(batch);
        }

        private async Task FlushAsync(List<AuditLog> entries)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.AuditLogs.AddRange(entries);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Audit batch ({Count} kayıt) DB'ye yazılamadı.", entries.Count);
            }
        }
    }
}
