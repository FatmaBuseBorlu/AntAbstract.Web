using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AntAbstract.Infrastructure.DesignTime
{
    public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                ?? Environment.GetEnvironmentVariable("PRODUCTION_CONNECTION_STRING")
                ?? "Server=(localdb)\\mssqllocaldb;Database=AntAbstractDesignTime;Trusted_Connection=True;TrustServerCertificate=True";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new AppDbContext(options, new TenantContext
            {
                IsGlobalContext = true
            });
        }
    }
}
