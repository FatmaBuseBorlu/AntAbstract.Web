using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AntAbstract.Web.DesignTime
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ResolveConnectionString())
                .Options;

            return new AppDbContext(options, new TenantContext());
        }

        private static string ResolveConnectionString()
        {
            var basePath = Directory.GetCurrentDirectory();
            var webProjectPath = Path.Combine(basePath, "AntAbstract.Web");
            var configurationPath = File.Exists(Path.Combine(basePath, "appsettings.json"))
                ? basePath
                : webProjectPath;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(configurationPath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return configuration.GetConnectionString("Default")
                ?? "Server=(localdb)\\mssqllocaldb;Database=AntAbstract;Trusted_Connection=True;TrustServerCertificate=True";
        }
    }
}
