using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>
/// "FATURA / INVOICE" başlıklı PDF, GİB'e bağlı olmadığı halde fatura gibi
/// duruyordu; artık ödeme makbuzu. Yönetici indirmesi erişilebilir kayıtlar
/// üzerinden yapılıyor; Stripe (Türkiye'de kullanılamıyor) anahtarı yokken
/// yönetim ekranında seçenek olarak çıkmıyor.
/// </summary>
public sealed class PaymentReceiptTests : IClassFixture<AuthenticatedTestFactory>
{
    private const string SlugA = "makbuz-kurum-a";
    private const string SlugB = "makbuz-kurum-b";

    private static readonly Guid TenantA = new("c1c1c1c1-0000-0000-0000-0000000000a1");
    private static readonly Guid TenantB = new("c1c1c1c1-0000-0000-0000-0000000000b1");
    private static readonly Guid ConferenceA = new("c2c2c2c2-0000-0000-0000-0000000000a2");
    private static readonly Guid ConferenceB = new("c2c2c2c2-0000-0000-0000-0000000000b2");
    private static readonly Guid RegistrationA = new("c3c3c3c3-0000-0000-0000-0000000000a3");
    private static readonly Guid RegistrationB = new("c3c3c3c3-0000-0000-0000-0000000000b3");

    private const string AdminA = "makbuz-admin-a";

    private readonly AuthenticatedTestFactory _factory;

    public PaymentReceiptTests(AuthenticatedTestFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantA))
            return;

        db.Tenants.Add(new Tenant { Id = TenantA, Slug = SlugA, Name = "Makbuz A" });
        db.Tenants.Add(new Tenant { Id = TenantB, Slug = SlugB, Name = "Makbuz B" });

        foreach (var (conf, tenant, slug) in new[] { (ConferenceA, TenantA, SlugA), (ConferenceB, TenantB, SlugB) })
        {
            db.Conferences.Add(new Conference
            {
                Id = conf,
                TenantId = tenant,
                Title = "Makbuz Kongresi " + slug,
                Slug = slug + "-kongre",
                StartDate = DateTime.Today.AddDays(20),
                EndDate = DateTime.Today.AddDays(21)
            });
        }

        db.Users.Add(NewUser(AdminA, TenantA));
        db.Users.Add(NewUser("makbuz-katilimci-a", null));
        db.Users.Add(NewUser("makbuz-katilimci-b", null));

        foreach (var (reg, conf, user) in new[]
                 {
                     (RegistrationA, ConferenceA, "makbuz-katilimci-a"),
                     (RegistrationB, ConferenceB, "makbuz-katilimci-b")
                 })
        {
            var typeId = Guid.NewGuid();
            db.RegistrationTypes.Add(new RegistrationType
            {
                Id = typeId,
                ConferenceId = conf,
                Name = "Katılımcı",
                Description = "Test",
                Price = 100,
                Currency = "TRY",
                IsActive = true,
                RoleName = "Listener"
            });

            db.Registrations.Add(new Registration
            {
                Id = reg,
                AppUserId = user,
                ConferenceId = conf,
                RegistrationTypeId = typeId,
                IsPaid = true,
                PaymentDate = DateTime.UtcNow,
                Amount = 100
            });

            db.Payments.Add(new Payment
            {
                AppUserId = user,
                ConferenceId = conf,
                Amount = 100,
                PaymentMethod = "BankTransfer",
                Status = PaymentStatus.Completed
            });
        }

        db.SaveChanges();
    }

    private static AppUser NewUser(string id, Guid? tenantId) => new()
    {
        Id = id,
        UserName = $"{id}@antabstract.local",
        NormalizedUserName = $"{id}@antabstract.local".ToUpperInvariant(),
        Email = $"{id}@antabstract.local",
        NormalizedEmail = $"{id}@antabstract.local".ToUpperInvariant(),
        FirstName = "Test",
        LastName = id,
        TenantId = tenantId,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private HttpClient AdminAClient()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Admin");
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AdminA);
        return client;
    }

    [Fact]
    public async Task Admin_DownloadsReceipt_ForOwnTenant()
    {
        var response = await AdminAClient().GetAsync($"/{SlugA}/Admin/Payments/DownloadInvoice/{RegistrationA}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Makbuz-", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Theory]
    [InlineData(SlugB)]
    [InlineData(SlugA)]
    public async Task Admin_CannotDownloadReceipt_OfAnotherTenant(string slug)
    {
        var response = await AdminAClient().GetAsync($"/{slug}/Admin/Payments/DownloadInvoice/{RegistrationB}");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConferenceEdit_HidesStripe_WhenNotConfigured()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync($"/{SlugA}/Admin/Conferences/Edit/{ConferenceA}");

        Assert.Contains("IsPayTREnabled", html);
        Assert.DoesNotContain("IsStripeEnabled", html);
    }

    [Fact]
    public void NewConference_DefaultsToPayTR()
    {
        var conference = new Conference();

        Assert.True(conference.IsPayTREnabled);
        Assert.False(conference.IsStripeEnabled);
    }
}
