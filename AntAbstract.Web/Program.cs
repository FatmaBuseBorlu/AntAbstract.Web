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
using Rotativa.AspNetCore;
using Microsoft.AspNetCore.Authentication.OAuth;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------
// I. SERVICES (Hizmet Tanýmlarý)
// ------------------------------------------------------

// Database
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.Password.RequiredLength = 6;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ORCID Harici Giriþ Saðlayýcýsý
builder.Services.AddAuthentication()
    .AddOAuth("ORCID", options =>
    {
        options.ClientId = builder.Configuration["ORCID:ClientId"] ?? throw new InvalidOperationException("ORCID ClientId bulunamadý.");
        options.ClientSecret = builder.Configuration["ORCID:ClientSecret"] ?? throw new InvalidOperationException("ORCID ClientSecret bulunamadý.");

        // HATA VEREN SATIR KALDIRILDI! (Artýk CS1061 hatasý almayacaksýnýz)
        // options.DisplayName = "ORCID ile Giriþ Yap"; 

        options.AuthorizationEndpoint = "https://orcid.org/oauth/authorize";
        options.TokenEndpoint = "https://orcid.org/oauth/token";
        options.UserInformationEndpoint = "https://pub.orcid.org/v3.0/oauth/userinfo";

        options.Scope.Add("/authenticate");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "orcid");
        options.SaveTokens = true;
        options.CallbackPath = "/signin-orcid";
    });

// Diðer Servisler
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IEmailSender, EmailService>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantResolver, SlugTenantResolver>();
builder.Services.AddScoped<IReviewerRecommendationService, ReviewerRecommendationService>();

// MVC, Views ve Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages();

// ------------------------------------------------------
// II. APPLICATION BUILD & SEEDING
// ------------------------------------------------------
var app = builder.Build();

// Ýlk DB Seeding
// Program.cs içinde DB Seeding bölümü:

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // YENÝ EKLENEN SATIR: Context servisini çaðýrýyoruz
        var context = services.GetRequiredService<AppDbContext>();

        // Parametre olarak context'i de gönderiyoruz
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
// III. MIDDLEWARE PIPELINE
// ------------------------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Localization
var supportedCultures = new[] { "tr-TR", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// ROUTING START
app.UseRouting();

// AUTHENTICATION & AUTHORIZATION
app.UseAuthentication();
app.UseAuthorization();

// TENANT MIDDLEWARE
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "tenant",
    pattern: "{tenant}/{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.UseRotativa();
app.Run();