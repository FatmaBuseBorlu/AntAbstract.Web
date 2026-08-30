using System.Security.Claims;
using System.Text.Encodings.Web;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Her isteği SuperAdmin olarak kimliklendirir. Böylece yetki duvarının
/// arkasındaki admin sayfalarının gerçekten render edilip edilmediği
/// (500 atmadığı) test edilebilir.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    /// <summary>
    /// Rolü ve kullanıcı kimliğini istek başlıklarıyla değiştirebilmek, yazar
    /// veya hakem gözünden gezinen testler yazmayı mümkün kılıyor.
    /// Başlık yoksa varsayılan SuperAdmin kimliği kullanılır.
    /// </summary>
    public const string RoleHeader = "X-Test-Role";
    public const string UserIdHeader = "X-Test-UserId";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers.TryGetValue(RoleHeader, out var r) && !string.IsNullOrWhiteSpace(r)
            ? r.ToString()
            : "SuperAdmin";

        var userId = Request.Headers.TryGetValue(UserIdHeader, out var u) && !string.IsNullOrWhiteSpace(u)
            ? u.ToString()
            : "test-superadmin";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId + "@antabstract.local"),
            new Claim(ClaimTypes.Email, userId + "@antabstract.local"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// İsteklere SuperAdmin kimliği takar ve veritabanı olarak bellekteki
/// SQLite'ı kullanır.
///
/// SQLite bilinçli bir tercih: InMemory sağlayıcı EF Core'un ilişkisel
/// sorgu çevirisini kullanmadığı için üretimde (SQL Server) sorunsuz
/// çalışan sorgularda hata verebiliyor. SQLite ilişkisel boru hattını
/// kullandığından testler üretim davranışına çok daha yakın olur.
/// </summary>
public sealed class AuthenticatedTestFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

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

            // Bağlantı açık kaldığı sürece bellekteki veritabanı yaşar.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
