using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services
{
    /// <summary>
    /// Audit log kayıtlarını AuditQueue'ya enqueue eder.
    /// Gerçek DB yazımı AuditWorker (BackgroundService) tarafından batch olarak yapılır.
    /// Bu sayede çağıran controller'ın request döngüsü DB I/O'suna bağımlı kalmaz.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly AuditQueue _queue;
        private readonly ILogger<AuditService> _logger;

        public AuditService(AuditQueue queue, ILogger<AuditService> logger)
        {
            _queue = queue;
            _logger = logger;
        }

        public Task LogAsync(
            string category,
            string action,
            string? userId = null,
            string? userName = null,
            string? entityType = null,
            string? entityId = null,
            string? description = null,
            Guid? conferenceId = null,
            string? ipAddress = null,
            string? oldValues = null,
            string? newValues = null)
        {
            var entry = new AuditLog
            {
                Category     = category,
                Action       = action,
                UserId       = userId,
                UserName     = userName,
                EntityType   = entityType,
                EntityId     = entityId,
                Description  = description,
                ConferenceId = conferenceId,
                IpAddress    = ipAddress,
                OldValues    = oldValues,
                NewValues    = newValues,
                CreatedAt    = DateTime.UtcNow
            };

            if (!_queue.TryEnqueue(entry))
            {
                // Kapasite aşıldı (10 000 kayıt) — sadece logla, akışı durdurma
                _logger.LogWarning(
                    "Audit queue kapasitesi aşıldı, kayıt düşürüldü: {Category}/{Action}",
                    category, action);
            }

            return Task.CompletedTask;
        }
    }
}
