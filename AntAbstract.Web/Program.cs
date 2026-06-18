using AntAbstract.Application.DependencyInjection;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Infrastructure.Services.Certficates;
using AntAbstract.Infrastructure.Services.Invoice;
using AntAbstract.Infrastructure.Services.Payment;
using AntAbstract.Infrastructure.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Rotativa.AspNetCore;
using Stripe;
using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Threading.RateLimiting;
using AntAbstract.Infrastructure.Services.ProceedingBooks;
using AntAbstract.Web.Files;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

#region 1. Veritaban� ve Temel Servisler

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IApplicationDbContext>(sp =>
    sp.GetRequiredService<AppDbContext>());

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

#endregion

#region 2. Kimlik Do�rulama ve Identity

builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    // Şifre politikası: en az 8 karakter, büyük harf, rakam zorunlu;
    // özel karakter isteğe bağlı bırakıldı (kullanıcı deneyimi korunuyor).
    opt.Password.RequiredLength = 8;
    opt.Password.RequireDigit = true;
    opt.Password.RequireUppercase = true;
    opt.Password.RequireLowercase = true;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequiredUniqueChars = 4;

    // Hesap kilitleme: 5 başarısız denemeden sonra 15 dakika kilitle
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    opt.Lockout.MaxFailedAccessAttempts = 5;
    opt.Lockout.AllowedForNewUsers = true;

    opt.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
});

builder.Services
    .AddAuthentication()
    .AddOpenIdConnect("ORCID", "ORCID", options =>
    {
        var authority = builder.Configuration["Authentication:ORCID:Authority"];
        var clientId = builder.Configuration["Authentication:ORCID:ClientId"];
        var clientSecret = builder.Configuration["Authentication:ORCID:ClientSecret"];

        options.Authority = string.IsNullOrWhiteSpace(authority)
            ? "https://orcid.org"
            : authority;

        options.ClientId = clientId;
        options.ClientSecret = clientSecret;

        options.CallbackPath = "/signin-orcid";

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;

        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        options.SignInScheme = IdentityConstants.ExternalScheme;

        options.Scope.Clear();
        options.Scope.Add("openid");

        options.TokenValidationParameters.NameClaimType = "name";

        options.ClaimActions.MapUniqueJsonKey("orcid", "sub");
        options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapUniqueJsonKey(ClaimTypes.GivenName, "given_name");
        options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Surname, "family_name");

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var orcidId =
                        context.Principal.FindFirst("sub")?.Value
                        ?? context.Principal.FindFirst("orcid")?.Value
                        ?? context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (!string.IsNullOrWhiteSpace(orcidId))
                    {
                        if (!identity.HasClaim(c => c.Type == "orcid"))
                        {
                            identity.AddClaim(new Claim("orcid", orcidId));
                        }
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AdminPolicies.TenantAdmin,
        policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new TenantAdminRequirement(allowSuperAdmin: true)));

    options.AddPolicy(
        AdminPolicies.TenantAdminOnly,
        policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new TenantAdminRequirement(allowSuperAdmin: false)));
});

#endregion

#region 3. Uygulama, Altyap� ve Tenant Servisleri

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

builder.Services.AddScoped<PdfCertificateService>();
builder.Services.AddScoped<IProceedingBookPdfService, ProceedingBookPdfService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<IVisaLetterPdfService, VisaLetterPdfService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IPayTRService, PayTRService>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantResolver, SlugTenantResolver>();
builder.Services.AddScoped<IAdminTenantAccessService, AdminTenantAccessService>();
builder.Services.AddScoped<IAuthorizationHandler, TenantAdminAuthorizationHandler>();
builder.Services.AddSingleton<IUploadFileValidator, UploadFileValidator>();

#endregion

#region 4. �oklu Dil ve MVC Ayarlar�

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = false;
    options.AppendTrailingSlash = false;
});

// Global upload limitleri.
// Kestrel ve FormOptions üst sınırı bildiri kitabına göre 52 MB ayarlıyoruz;
// bireysel action'lar [RequestSizeLimit] / [RequestFormLimits] ile daha düşük
// sınır uygular (örn. bildiri PDF ve makbuz için 20 MB).
// Bu sayede Kestrel isteği middleware katmanında kesmez; limit kontrolü
// controller seviyesinde yapılır.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52 * 1024 * 1024; // 52 MB (bildiri kitabı için üst sınır)
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = 52 * 1024 * 1024; // 52 MB
});

// ── Response Compression ─────────────────────────────────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    // text/html kasıtlı olarak dışarıda: authenticated HTML + CSRF token
    // içeren sayfalar BREACH saldırısına karşı sıkıştırılmıyor.
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/css", "text/javascript", "application/javascript",
        "application/json", "image/svg+xml", "font/woff2"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// ── Rate Limiting ────────────────────────────────────────────────────────────
// IP bazlı sliding-window limitleri. Development'ta daha gevşek tutulur.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login / Register / ForgotPassword: 10 istek / 5 dakika / IP
    options.AddSlidingWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(5);
        opt.SegmentsPerWindow = 5;
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Dosya/makbuz yükleme: 20 istek / dakika / IP
    options.AddSlidingWindowLimiter("upload", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.PermitLimit = 20;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Stripe webhook: 60 istek / dakika (Stripe yeniden dener)
    options.AddSlidingWindowLimiter("webhook", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
        opt.PermitLimit = 60;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddAreaPageRoute(
        areaName: "Identity",
        pageName: "/Account/Login",
        route: "login"
    );

    options.Conventions.AddAreaPageRoute(
        areaName: "Identity",
        pageName: "/Account/Register",
        route: "register"
    );

    options.Conventions.AddAreaPageRoute(
        areaName: "Identity",
        pageName: "/Account/ForgotPassword",
        route: "forgot-password"
    );

    options.Conventions.AddAreaPageRoute(
        areaName: "Identity",
        pageName: "/Account/AccessDenied",
        route: "access-denied"
    );
});

#endregion

var app = builder.Build();

#region 5. Veritaban� Ba�latma ve Ayarlar

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var startupLogger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<AppDbContext>();

        // Production'da otomatik migration DEVRE DIŞI bırakıldı.
        // Migration'lar deployment pipeline'ında (CI/CD) çalıştırılmalıdır:
        //   dotnet ef database update --project AntAbstract.Infrastructure \
        //     --startup-project AntAbstract.Web
        // Development ortamında kolaylık için migration uygulanır.
        // Testing ortamında (InMemory) relational migration API'si yoktur, atlanır.
        if (app.Environment.IsDevelopment())
        {
            await context.Database.MigrateAsync();
        }
        else if (!app.Environment.IsEnvironment("Testing"))
        {
            // Production'da veritabanının güncel olduğunu doğrula; değilse başlatmayı durdur
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                startupLogger.LogCritical(
                    "Bekleyen {Count} migration var: {Names}. Uygulamayı başlatmadan önce " +
                    "'dotnet ef database update' komutunu çalıştırın.",
                    pendingMigrations.Count(),
                    string.Join(", ", pendingMigrations));
                throw new InvalidOperationException("Veritabanı şeması güncel değil. Uygulama başlatılamadı.");
            }
        }

        await AntAbstract.Infrastructure.Data.DbInitializer.Initialize(
            userManager,
            roleManager,
            context
        );

        await AntAbstract.Infrastructure.Data.DbSeeder.SeedRolesAndUsers(services);

        if (app.Environment.IsDevelopment())
        {
            await AntAbstract.Infrastructure.Data.TestDataSeeder.SeedAsync(userManager, context);
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Başlangıç sırasında hata oluştu.");
        throw; // Uygulama kötü bir durumda başlamasın
    }
}

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

#endregion

#region 6. HTTP �stek Hatt�

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseResponseCompression();

app.UseHttpsRedirection();

// ── Security Headers ─────────────────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;

    // Clickjacking koruması
    h["X-Frame-Options"] = "SAMEORIGIN";

    // MIME-sniffing koruması
    h["X-Content-Type-Options"] = "nosniff";

    // Referer bilgisi kontrolü
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Tarayıcı özellik erişim kısıtlamaları
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(self)";

    // Content Security Policy
    // 'unsafe-inline' script: Stripe.js, Bootstrap tooltips ve mevcut inline scriptler
    // için gerekli — hashes ile kaldırılabilir ileride.
    h["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://js.stripe.com https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' https://api.stripe.com; " +
        "frame-src https://js.stripe.com https://hooks.stripe.com; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';";

    await next();
});

// UseRateLimiter() UseRouting()'den sonra konumlandırılmıştır çünkü
// named policy'lerin ([EnableRateLimiting]) endpoint metadata'yı okuyabilmesi
// için routing'in daha önce çalışması gerekir.

// Hassas dosyalar (submissions, receipts, templates) artık wwwroot dışında
// ContentRoot/private-uploads altında tutulur; SecureDownloadController ile serve edilir.
// wwwroot'ta yalnızca herkese açık statik varlıklar kaldı.
// CSS/JS/font'lar için 30 günlük cache; HTML değiştiğinde yeni fingerprint (bundler/versioning ile).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        var ext = Path.GetExtension(path).ToLowerInvariant();

        // Versiyonlanmış varlıklar (css, js, font, resim) uzun süre cache'lenebilir
        if (ext is ".css" or ".js" or ".woff" or ".woff2" or ".ttf" or ".eot"
                or ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".svg" or ".webp")
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=2592000, immutable"; // 30 gün
        }
    }
});

var supportedCultures = new[] { "tr-TR", "en-US" };

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("tr-TR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new CookieRequestCultureProvider(),
    new QueryStringRequestCultureProvider(),
    new AcceptLanguageHeaderRequestCultureProvider()
};

app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseRateLimiter(); // UseRouting'den SONRA — named policy'ler endpoint metadata'ya ihtiyaç duyar

app.UseSession();

app.UseAuthentication();

// Tenant middleware: UseAuthentication'dan SONRA, UseAuthorization'dan ÖNCE olmalı.
// TenantAdminRequirement gibi authorization handler'ları TenantContext'i okur;
// bu sıra sayesinde policy değerlendirmesi sırasında tenant zaten set edilmiş olur.
app.Use(async (ctx, next) =>
{
    var resolver = ctx.RequestServices.GetRequiredService<ITenantResolver>();
    var tenantContext = ctx.RequestServices.GetRequiredService<TenantContext>();

    tenantContext.Current = await resolver.ResolveAsync(ctx);

    // Slug bulunamazsa (global rota) ve kullanıcı SuperAdmin ise
    // tüm tenant verisine erişime izin ver (güvenli global bağlam).
    // Aksi hâlde null CurrentTenantId hiçbir veri döndürmez.
    if (tenantContext.Current == null && ctx.User.IsInRole("SuperAdmin"))
    {
        tenantContext.IsGlobalContext = true;
    }

    await next();
});

app.UseAuthorization();

app.UseRotativa();

#endregion

#region 7. Y�nlendirmeler

app.MapRazorPages();

app.MapControllerRoute(
    name: "public_congresses",
    pattern: "congresses",
    defaults: new
    {
        controller = "Home",
        action = "Congresses"
    });

app.MapControllerRoute(
    name: "public_about",
    pattern: "about",
    defaults: new
    {
        controller = "Home",
        action = "About"
    });

app.MapControllerRoute(
    name: "public_contact",
    pattern: "contact",
    defaults: new
    {
        controller = "Home",
        action = "Contact"
    });

app.MapControllerRoute(
    name: "public_proceedings",
    pattern: "proceedings",
    defaults: new
    {
        controller = "Home",
        action = "Proceedings"
    });

app.MapControllerRoute(
    name: "public_privacy",
    pattern: "privacy",
    defaults: new
    {
        controller = "Home",
        action = "Privacy"
    });

app.MapControllerRoute(
    name: "public_kvkk",
    pattern: "kvkk",
    defaults: new
    {
        controller = "Home",
        action = "Kvkk"
    });

app.MapControllerRoute(
    name: "public_cookies",
    pattern: "cookies",
    defaults: new
    {
        controller = "Home",
        action = "Cookies"
    });

app.MapControllerRoute(
    name: "public_terms",
    pattern: "terms",
    defaults: new
    {
        controller = "Home",
        action = "Terms"
    });

app.MapControllerRoute(
    name: "dashboard_slug",
    pattern: "{slug}/Dashboard/{action=Index}/{id?}",
    defaults: new
    {
        controller = "Dashboard"
    });

app.MapControllerRoute(
    name: "dashboard_root",
    pattern: "Dashboard/{action=Index}/{id?}",
    defaults: new
    {
        controller = "Dashboard"
    });

app.MapControllerRoute(
    name: "tenant_areas",
    pattern: "{slug}/{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "tenant",
    pattern: "{slug}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

#endregion

app.Run();
public partial class Program { }
