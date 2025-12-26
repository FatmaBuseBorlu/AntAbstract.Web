using AntAbstract.Application.Interfaces;
using AntAbstract.Application.Mappings;
using AntAbstract.Application.Services;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using AntAbstract.Web.StartupServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using Stripe;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = false;
    options.AppendTrailingSlash = false;
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

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IEmailSender, EmailService>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantResolver, SlugTenantResolver>();

builder.Services.AddScoped<IReviewerRecommendationService, ReviewerRecommendationService>();
builder.Services.AddScoped<ISubmissionService, SubmissionManager>();
builder.Services.AddScoped<IReviewService, ReviewManager>();
builder.Services.AddScoped<ISelectedConferenceService, SelectedConferenceService>();

builder.Services.AddAutoMapper(typeof(GeneralMappingProfile));

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<AppDbContext>();

        await AntAbstract.Infrastructure.Data.DbInitializer.Initialize(userManager, roleManager, context);
        await AntAbstract.Infrastructure.Data.DbSeeder.SeedRolesAndUsers(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Seeding sýrasýnda bir hata oluþtu.");
    }
}

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

if (!app.Environment.IsDevelopment())
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

app.MapRazorPages();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "tenant_areas",
    pattern: "{slug}/{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "tenant",
    pattern: "{slug}/{controller=Home}/{action=Index}/{id?}");

app.MapControllers();


app.Run();
