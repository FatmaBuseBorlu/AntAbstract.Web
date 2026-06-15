using AntAbstract.Application.DependencyInjection;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Infrastructure.Services.Certficates;
using AntAbstract.Infrastructure.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Rotativa.AspNetCore;
using Stripe;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Localization;
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
    opt.Password.RequiredLength = 6;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = false;
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

// Global form / dosya upload limitleri
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024; // 20 MB (bildiri PDF)
    options.ValueLengthLimit            = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = 20 * 1024 * 1024; // 20 MB
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

    try
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync();

        await AntAbstract.Infrastructure.Data.DbInitializer.Initialize(
            userManager,
            roleManager,
            context
        );

        await AntAbstract.Infrastructure.Data.DbSeeder.SeedRolesAndUsers(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Migration/Seeding s�ras�nda bir hata olu�tu.");
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

app.UseHttpsRedirection();

// Hassas upload klasörlerine doğrudan erişimi engelle
// (submissions, receipts, templates) — profil resimleri ve proceeding-books herkese açık
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        var blocked = new[] { "/uploads/submissions/", "/uploads/receipts/", "/uploads/templates/" };
        if (blocked.Any(b => path.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
        {
            ctx.Context.Response.StatusCode = 403;
            ctx.Context.Response.Headers["Cache-Control"] = "no-store";
            ctx.Context.Response.ContentLength = 0;
            ctx.Context.Response.Body = System.IO.Stream.Null;
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

app.UseSession();

app.Use(async (ctx, next) =>
{
    var resolver = ctx.RequestServices.GetRequiredService<ITenantResolver>();
    var tenantContext = ctx.RequestServices.GetRequiredService<TenantContext>();

    tenantContext.Current = await resolver.ResolveAsync(ctx);

    await next();
});

app.UseAuthentication();
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
