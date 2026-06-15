using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services
{
    /// <summary>
    /// Denetim kayıtlarını veritabanına yazar.
    /// Kendi scope'unda çalışır; çağıran controller'ın DbContext'inden bağımsızdır.
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IServiceScopeFactory scopeFactory, ILogger<AuditService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task LogAsync(
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
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var log = new AuditLog
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

                db.AuditLogs.Add(log);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Audit hatası asla ana akışı kesmemeli
                _logger.LogWarning(ex, "Audit log kaydedilemedi: {Category}/{Action}", category, action);
            }
        }
    }
}
