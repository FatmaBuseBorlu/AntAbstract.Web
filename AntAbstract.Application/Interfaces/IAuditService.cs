using System;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public interface IAuditService
    {
        /// <summary>
        /// Denetim kaydı oluşturur. Fire-and-forget kullanımına uygundur.
        /// </summary>
        Task LogAsync(
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
            string? newValues = null);
    }
}
