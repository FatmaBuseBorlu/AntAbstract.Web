using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AntAbstract.Web.Security
{
    // Anahtarsız /health için: yalnızca veritabanına bağlanılabiliyor mu bakar.
    // Ayrıntılı durum (Stripe, PayTR, SMTP) anahtarlı /Admin/Health/Status'ta kalır.
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;

        public DatabaseHealthCheck(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy();
            }
            catch
            {
                return HealthCheckResult.Unhealthy();
            }
        }
    }
}
