using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.StartupServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Security.Claims;
using Rotativa.AspNetCore; // PDF motoru için
using Microsoft.AspNetCore.Authentication.OAuth; // OAuth ayarlarý için

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------
// I. SERVICES (Hizmet Tanýmlarý)
// ------------------------------------------------------

// 1. Veritabaný Baðlantýsý
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// 2. Identity Ayarlarý
builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.Password.RequiredLength = 6;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = false;
    opt.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 3. Login Yönlendirme Ayarý (Kayýt Ol butonunun doðru çalýþmasý için ÞART)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// 4. ORCID Harici Giriþ Saðlayýcýsý
builder.Services.AddAuthentication()
    .AddOAuth("ORCID", options =>
    {
        options.ClientId = builder.Configuration["ORCID:ClientId"] ?? throw new InvalidOperationException("ORCID ClientId bulunamadý.");
        options.ClientSecret = builder.Configuration["ORCID:ClientSecret"] ?? throw new InvalidOperationException("ORCID ClientSecret bulunamadý.");

        // Not: DisplayName özelliði OAuthOptions'da yoktur, bu ayarý View (Login.cshtml) tarafýnda hallettik.

        options.AuthorizationEndpoint = "https://orcid.org/oauth/authorize";
        options.TokenEndpoint = "https://orcid.org/oauth/token";
        options.UserInformationEndpoint = "https://pub.orcid.org/v3.0/oauth/userinfo";

        options.Scope.Add("/authenticate");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "orcid");
        options.SaveTokens = true;
        options.CallbackPath = "/signin-orcid";
    });

// 5. Diðer Servisler (Email, Tenant, Stripe)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IEmailSender, EmailService>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantResolver, SlugTenantResolver>();
builder.Services.AddScoped<IReviewerRecommendationService, ReviewerRecommendationService>();

// 6. MVC ve Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages();

// ------------------------------------------------------
// II. APPLICATION BUILD & SEEDING
// ------------------------------------------------------
var app = builder.Build();

// Veritabaný Baþlangýç Verileri (Seeding)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<AppDbContext>(); // Context eklendi

        // DbInitializer'ý çalýþtýr (Roller, Admin ve Örnek Kongreler)
        await AntAbstract.Infrastructure.Data.DbInitializer.Initialize(userManager, roleManager, context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabaný oluþturulurken bir hata oluþtu (Seeding).");
    }
}

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// ------------------------------------------------------
// III. MIDDLEWARE PIPELINE (Ýstek Ýþleme Sýrasý)
// ------------------------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Dil Ayarlarý
var supportedCultures = new[] { "tr-TR", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Yönlendirme
app.UseRouting();

// Kimlik Doðrulama & Yetkilendirme
app.UseAuthentication();
app.UseAuthorization();

// Tenant Middleware (Yetkilendirmeden sonra)
app.Use(async (ctx, next) =>
{
    var resolver = ctx.RequestServices.GetRequiredService<ITenantResolver>();
    var tc = ctx.RequestServices.GetRequiredService<TenantContext>();
    tc.Current = await resolver.ResolveAsync(ctx.Request);

    await next();
});

// ------------------------------------------------------
// IV. ENDPOINT ROUTING
// ------------------------------------------------------

// 1. Varsayýlan Rota (Ana Site)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 2. Tenant Rotasý (Kongre Siteleri)
app.MapControllerRoute(
    name: "tenant",
    pattern: "{tenant}/{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// PDF Motorunu Baþlat (wwwroot/Rotativa klasörü dolu olmalý)
app.UseRotativa();

app.Run();