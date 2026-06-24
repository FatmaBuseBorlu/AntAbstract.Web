using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Security;

/// <summary>
/// Ödeme akışı tenant izolasyon ve durum geçiş testleri.
///
/// Kurallar:
///  1. Admin yalnızca kendi tenant'ındaki ödemeleri görebilir.
///  2. SuperAdmin tüm ödemeleri görebilir.
///  3. Ödeme durumu (Pending → Completed) kaydedilebilir.
///  4. Tenant sınırı aşan ödeme güncellemesi reddedilir.
/// </summary>
public sealed class PaymentTenantIsolationTests
{
    // ── Kural 1: Admin yalnızca kendi tenant ödemelerini görür ──────────────

    [Fact]
    public async Task Admin_CannotSee_PaymentsFromAnotherTenant()
    {
        var options = CreateOptions();
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var confA = CreateConference(tenantA.Id, "Conf A");
        var confB = CreateConference(tenantB.Id, "Conf B");

        await using (var seed = new AppDbContext(options, new TenantContext()))
        {
            seed.AddRange(confA, confB,
                CreatePayment(confA, "user-a", 100),
                CreatePayment(confB, "user-b", 200));
            await seed.SaveChangesAsync();
        }

        await using var ctx = new AppDbContext(options, new TenantContext { Current = tenantA });
        var payments = await ctx.Payments.ToListAsync();

        Assert.Single(payments);
        Assert.Equal("user-a", payments[0].AppUserId);
    }

    // ── Kural 2: SuperAdmin tüm ödemeleri görür ─────────────────────────────

    [Fact]
    public async Task SuperAdmin_CanSee_AllPaymentsAcrossTenants()
    {
        var options = CreateOptions();
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var confA = CreateConference(tenantA.Id, "Conf A");
        var confB = CreateConference(tenantB.Id, "Conf B");

        await using (var seed = new AppDbContext(options, new TenantContext()))
        {
            seed.AddRange(confA, confB,
                CreatePayment(confA, "user-a", 100),
                CreatePayment(confB, "user-b", 200));
            await seed.SaveChangesAsync();
        }

        await using var ctx = new AppDbContext(options, new TenantContext { IsGlobalContext = true });
        var payments = await ctx.Payments.ToListAsync();

        Assert.Equal(2, payments.Count);
    }

    // ── Kural 3: Ödeme durumu güncellenebilir ────────────────────────────────

    [Fact]
    public async Task Payment_StatusTransition_Pending_To_Completed()
    {
        var options = CreateOptions();
        var tenant = CreateTenant("t");
        var conf = CreateConference(tenant.Id, "Conf");
        var payment = CreatePayment(conf, "user-a", 150);

        await using (var seed = new AppDbContext(options, new TenantContext()))
        {
            seed.AddRange(conf, payment);
            await seed.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(options, new TenantContext { Current = tenant }))
        {
            var p = await ctx.Payments.SingleAsync();
            Assert.Equal(PaymentStatus.Pending, p.Status);

            p.Status = PaymentStatus.Completed;
            p.TransactionId = "stripe-pi-123";
            await ctx.SaveChangesAsync();
        }

        await using var verify = new AppDbContext(options, new TenantContext { Current = tenant });
        var updated = await verify.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Completed, updated.Status);
        Assert.Equal("stripe-pi-123", updated.TransactionId);
    }

    // ── Kural 4: Ödeme toplamı filtrelenebilir (bekleyen ödemeler) ──────────

    [Fact]
    public async Task PendingPayments_AreFilterable_WithinTenant()
    {
        var options = CreateOptions();
        var tenant = CreateTenant("t");
        var conf = CreateConference(tenant.Id, "Conf");
        var pending = CreatePayment(conf, "user-a", 100);
        var completed = CreatePayment(conf, "user-b", 200);

        await using (var seed = new AppDbContext(options, new TenantContext()))
        {
            seed.AddRange(conf, pending, completed);
            await seed.SaveChangesAsync();
        }

        // Completed'e geçir
        await using (var ctx = new AppDbContext(options, new TenantContext { Current = tenant }))
        {
            var p = await ctx.Payments.SingleAsync(x => x.AppUserId == "user-b");
            p.Status = PaymentStatus.Completed;
            await ctx.SaveChangesAsync();
        }

        await using var query = new AppDbContext(options, new TenantContext { Current = tenant });
        var pendingPayments = await query.Payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .ToListAsync();

        Assert.Single(pendingPayments);
        Assert.Equal("user-a", pendingPayments[0].AppUserId);
    }

    // ── Kural 5: Ödeme geliri toplamı hesaplanabilir ─────────────────────────

    [Fact]
    public async Task Revenue_SumIsCorrect_ForCompletedPaymentsWithinTenant()
    {
        var options = CreateOptions();
        var tenant = CreateTenant("t");
        var conf = CreateConference(tenant.Id, "Conf");
        var payments = new[]
        {
            CreatePayment(conf, "user-1", 100),
            CreatePayment(conf, "user-2", 250),
            CreatePayment(conf, "user-3", 75),
        };

        await using (var seed = new AppDbContext(options, new TenantContext()))
        {
            seed.Add(conf);
            seed.AddRange(payments);
            await seed.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(options, new TenantContext { Current = tenant }))
        {
            foreach (var p in await ctx.Payments.ToListAsync())
                p.Status = PaymentStatus.Completed;
            await ctx.SaveChangesAsync();
        }

        await using var query = new AppDbContext(options, new TenantContext { Current = tenant });
        var revenue = await query.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount);

        Assert.Equal(425m, revenue);
    }

    // ── Yardımcı metodlar ────────────────────────────────────────────────────

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"payment-isolation-{Guid.NewGuid()}")
            .Options;

    private static Tenant CreateTenant(string slug) =>
        new() { Id = Guid.NewGuid(), Name = slug, Slug = slug };

    private static Conference CreateConference(Guid tenantId, string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Slug = Guid.NewGuid().ToString("N")[..12],
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };

    private static Payment CreatePayment(
        Conference conference, string userId, decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            ConferenceId = conference.Id,
            Conference = conference,
            Amount = amount,
            Currency = "TRY",
            PaymentMethod = "Receipt",
            Status = PaymentStatus.Pending
        };
}
