using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.StartupServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// --- Servis Konfigürasyonlarý ---

// Veritabaný ve EF Core
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity (Kullanýcý Sistemi)
builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.Password.RequiredLength = 6;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Email Ayarlarýný appsettings.json'dan yükle
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
// Email Servisini sisteme tanýt
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IEmailSender, EmailService>();

// Multi-Tenant (Kongre) Altyapýsý
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantResolver, SlugTenantResolver>();
builder.Services.AddScoped<IReviewerRecommendationService, ReviewerRecommendationService>();

// Çoklu Dil Desteði (Localization)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages();

// --- Uygulamanýn Ýnþa Edilmesi ---
var app = builder.Build();
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Tenant (Kongre) Middleware'i - HER ORTAMDA ÇALIÞMALI
app.Use(async (ctx, next) =>
{
    var resolver = ctx.RequestServices.GetRequiredService<ITenantResolver>();
    var tc = ctx.RequestServices.GetRequiredService<TenantContext>();
    tc.Current = await resolver.ResolveAsync(ctx.Request);
    await next();
});

// Hata Yönetimi
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Çoklu Dil Middleware'i
var supportedCultures = new[] { "tr-TR", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Rotalama
app.MapControllerRoute(
    name: "tenant",
    pattern: "{tenant}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Veritabaný Baþlatýcý (Rolleri vb. oluþturur)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await IdentityDbInitializer.InitializeAsync(services);
}

// Uygulamayý Çalýþtýr
app.Run();