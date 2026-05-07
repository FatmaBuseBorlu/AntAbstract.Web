using AntAbstract.Application.DependencyInjection;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Infrastructure.Services.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using Stripe;
using System.Globalization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

#region 1. Veritabaný ve Temel Servisler

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

#region 2. Kimlik Doðrulama ve Identity

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

builder.Services.AddAuthentication()
    .AddOAuth("ORCID", options =>
    {
        options.ClientId = builder.Configuration["ORCID:ClientId"]
            ?? throw new InvalidOperationException("ORCID ClientId bulunamadý.");

        options.ClientSecret = builder.Configuration["ORCID:ClientSecret"]
            ?? throw new InvalidOperationException("ORCID ClientSecret bulunamadý.");

        options.AuthorizationEndpoint = "https://orcid.org/oauth/authorize";
        options.TokenEndpoint = "https://orcid.org/oauth/token";
        options.UserInformationEndpoint = "https://pub.orcid.org/v3.0/oauth/userinfo";

        options.Scope.Add("/authenticate");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "orcid");
        options.SaveTokens = true;
        options.CallbackPath = "/signin-orcid";
    });

#endregion

#region 3. Uygulama, Altyapý ve Tenant Servisleri

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantResolver, SlugTenantResolver>();

#endregion

#region 4. Çoklu Dil (Localization) ve MVC Ayarlarý

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = false;
    options.AppendTrailingSlash = false;
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

#region 5. Veritabaný Baþlatma (Migration + Seeding) ve Ayarlar

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
        logger.LogError(ex, "Migration/Seeding sýrasýnda bir hata oluþtu.");
    }
}

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

#endregion

#region 6. HTTP Ýstek Hattý (Middleware Pipeline)

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
app.UseStaticFiles();

var supportedCultures = new[] { "tr-TR", "en-US" };

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseRouting();
app.UseSession();

app.Use(async (ctx, next) =>
{
    var resolver = ctx.RequestServices.GetRequiredService<ITenantResolver>();
    var tc = ctx.RequestServices.GetRequiredService<TenantContext>();

    tc.Current = await resolver.ResolveAsync(ctx);

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.UseRotativa();

#endregion

#region 7. Yönlendirmeler (Endpoints)

app.MapRazorPages();

/*
    Public temiz URL'ler

    Eski URL'ler:
    /Home/Congresses
    /Home/About
    /Home/Contact
    /Home/Proceedings

    Yeni URL'ler:
    /congresses
    /about
    /contact
    /proceedings
*/

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