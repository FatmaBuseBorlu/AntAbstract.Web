using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Web.Tests.Smoke;

/// <summary>
/// SmokeTestFactory'den farkı: veritabanı adı fabrika ömrü boyunca sabittir,
/// böylece testte tohumlanan veri istek sırasında da görünür.
/// </summary>
public sealed class RegistrationAvailabilityFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "RegAvail-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Critical);
        });
    }

    /// <summary>
    /// Tenant filtrelerini atlayarak test verisi ekler.
    /// </summary>
    public void Seed(Action<AppDbContext> seed)
    {
        using var scope = Services.CreateScope();

        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.IsGlobalContext = true;

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        seed(db);

        db.SaveChanges();
    }

    /// <summary>
    /// Bir kongre ve ona bağlı tek bir aktif kayıt türü oluşturur.
    /// </summary>
    public (Tenant Tenant, Conference Conference) SeedConference(
        string slug,
        bool isRegistrationOpen = true,
        DateTime? endDate = null,
        int? maxRegistrations = null,
        DateTime? registrationTypeDeadline = null,
        bool registrationTypeActive = true,
        int existingRegistrationCount = 0)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = slug
        };

        var conference = new Conference
        {
            Id = Guid.NewGuid(),
            Title = $"{slug} Kongresi",
            Slug = slug,
            TenantId = tenant.Id,
            StartDate = DateTime.UtcNow.Date.AddDays(20),
            EndDate = endDate ?? DateTime.UtcNow.Date.AddDays(22),
            IsRegistrationOpen = isRegistrationOpen,
            MaxRegistrations = maxRegistrations
        };

        var registrationType = new RegistrationType
        {
            Id = Guid.NewGuid(),
            ConferenceId = conference.Id,
            Name = "Akademisyen",
            Price = 1000,
            Currency = "TRY",
            IsActive = registrationTypeActive,
            Deadline = registrationTypeDeadline
        };

        Seed(db =>
        {
            db.Tenants.Add(tenant);
            db.Conferences.Add(conference);
            db.RegistrationTypes.Add(registrationType);

            for (var i = 0; i < existingRegistrationCount; i++)
            {
                db.Registrations.Add(new Registration
                {
                    Id = Guid.NewGuid(),
                    AppUserId = $"seed-user-{i}",
                    ConferenceId = conference.Id,
                    RegistrationTypeId = registrationType.Id,
                    Amount = 1000,
                    RegistrationDate = DateTime.UtcNow
                });
            }
        });

        return (tenant, conference);
    }
}
