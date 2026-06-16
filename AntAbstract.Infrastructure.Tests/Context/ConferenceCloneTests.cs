using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Context;

/// <summary>
/// Kongre kopyalama (Clone) işleminin doğruluğunu test eder:
/// - Konular (ConferenceTopic) eksiksiz kopyalanır
/// - Kayıt türleri (RegistrationType) eksiksiz kopyalanır
/// - Sayfa blokları (ConferencePageBlock) eksiksiz kopyalanır
/// - Kayıtlar / ödemeler kopyalanmaz
/// - Klonun Id'si kaynaktan farklı olur
/// </summary>
public sealed class ConferenceCloneTests
{
    [Fact]
    public async Task Clone_CopiesAllTopicFields()
    {
        var options = CreateOptions();
        var (tenant, source) = await SeedSourceAsync(options);

        var clone = await RunCloneAsync(options, tenant, source.Id);

        var cloneTopics = await GetTopicsAsync(options, tenant, clone.Id);

        Assert.Equal(2, cloneTopics.Count);

        var first = cloneTopics.OrderBy(t => t.SortOrder).First();
        Assert.Equal("Yapay Zeka", first.Name);
        Assert.Equal("Artificial Intelligence", first.NameEn);
        Assert.Equal("YZ açıklaması", first.Description);
        Assert.Equal("AI description", first.DescriptionEn);
        Assert.Equal(1, first.SortOrder);
        Assert.True(first.IsActive);
    }

    [Fact]
    public async Task Clone_CopiesAllRegistrationTypeFields()
    {
        var options = CreateOptions();
        var (tenant, source) = await SeedSourceAsync(options);

        var clone = await RunCloneAsync(options, tenant, source.Id);

        var cloneRegTypes = await GetRegTypesAsync(options, tenant, clone.Id);

        Assert.Equal(2, cloneRegTypes.Count);

        var student = cloneRegTypes.Single(r => r.Name == "Öğrenci");
        Assert.Equal("Student", student.NameEn);
        Assert.Equal("Öğrenci kayıt açıklaması", student.Description);
        Assert.Equal("Student registration description", student.DescriptionEn);
        Assert.Equal(150m, student.Price);
        Assert.Equal("TRY", student.Currency);
        Assert.Equal("Author", student.RoleName);
    }

    [Fact]
    public async Task Clone_CopiesAllPageBlockFields()
    {
        var options = CreateOptions();
        var (tenant, source) = await SeedSourceAsync(options);

        var clone = await RunCloneAsync(options, tenant, source.Id);

        var cloneBlocks = await GetPageBlocksAsync(options, tenant, clone.Id);

        Assert.Equal(2, cloneBlocks.Count);

        var hero = cloneBlocks.Single(b => b.BlockType == ConferencePageBlockType.Hero);
        Assert.Equal("Home", hero.Page);
        Assert.Equal("tr-TR", hero.Culture);
        Assert.Equal("Ana Sayfa Başlık", hero.Title);
        Assert.Equal("{\"bg\":\"blue\"}", hero.ContentJson);
        Assert.True(hero.IsActive);
        Assert.Equal(clone.Id, hero.ConferenceId);
        Assert.Equal(clone.TenantId, hero.TenantId);
    }

    [Fact]
    public async Task Clone_HasDifferentId_FromSource()
    {
        var options = CreateOptions();
        var (tenant, source) = await SeedSourceAsync(options);

        var clone = await RunCloneAsync(options, tenant, source.Id);

        Assert.NotEqual(source.Id, clone.Id);
    }

    [Fact]
    public async Task Clone_DoesNotCopyRegistrationsOrPayments()
    {
        var options = CreateOptions();
        var (tenant, source) = await SeedSourceAsync(options);

        var clone = await RunCloneAsync(options, tenant, source.Id);

        await using var ctx = new AppDbContext(options, new TenantContext { Current = tenant });
        var cloneRegistrations = await ctx.Registrations
            .Where(r => r.ConferenceId == clone.Id)
            .CountAsync();
        var clonePayments = await ctx.Payments
            .Where(p => p.ConferenceId == clone.Id)
            .CountAsync();

        Assert.Equal(0, cloneRegistrations);
        Assert.Equal(0, clonePayments);
    }

    [Fact]
    public async Task Clone_SourceDataRemainsIntact()
    {
        var options = CreateOptions();
        var (tenant, source) = await SeedSourceAsync(options);

        await RunCloneAsync(options, tenant, source.Id);

        var sourceTopics = await GetTopicsAsync(options, tenant, source.Id);
        var sourceRegTypes = await GetRegTypesAsync(options, tenant, source.Id);
        var sourceBlocks = await GetPageBlocksAsync(options, tenant, source.Id);

        Assert.Equal(2, sourceTopics.Count);
        Assert.Equal(2, sourceRegTypes.Count);
        Assert.Equal(2, sourceBlocks.Count);
    }

    // ── Yardımcı metodlar ────────────────────────────────────────────────────

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"clone-{Guid.NewGuid()}")
            .Options;

    /// <summary>
    /// Kaynak kongre ve bağlı verileri seed eder.
    /// Seed için global context kullanılır (query filter bypass).
    /// </summary>
    private static async Task<(Tenant, Conference)> SeedSourceAsync(
        DbContextOptions<AppDbContext> options)
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Test", Slug = "test" };
        var source = new Conference
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Title = "Kaynak Kongre",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(3)
        };
        var regType1 = new RegistrationType
        {
            Id = Guid.NewGuid(), ConferenceId = source.Id, Conference = source,
            Name = "Öğrenci", NameEn = "Student",
            Description = "Öğrenci kayıt açıklaması",
            DescriptionEn = "Student registration description",
            Price = 150m, Currency = "TRY", RoleName = "Author"
        };
        var regType2 = new RegistrationType
        {
            Id = Guid.NewGuid(), ConferenceId = source.Id, Conference = source,
            Name = "Akademisyen", NameEn = "Academic",
            Description = "Akademisyen açıklaması",
            DescriptionEn = "Academic description",
            Price = 300m, Currency = "TRY", RoleName = "Author"
        };
        var topic1 = new ConferenceTopic
        {
            Id = Guid.NewGuid(), ConferenceId = source.Id,
            Name = "Yapay Zeka", NameEn = "Artificial Intelligence",
            Description = "YZ açıklaması", DescriptionEn = "AI description",
            SortOrder = 1, IsActive = true
        };
        var topic2 = new ConferenceTopic
        {
            Id = Guid.NewGuid(), ConferenceId = source.Id,
            Name = "Veri Bilimi", NameEn = "Data Science",
            SortOrder = 2, IsActive = true
        };
        var block1 = new ConferencePageBlock
        {
            TenantId = tenant.Id, ConferenceId = source.Id,
            Page = "Home", Culture = "tr-TR",
            BlockType = ConferencePageBlockType.Hero,
            Title = "Ana Sayfa Başlık", ContentJson = "{\"bg\":\"blue\"}",
            Order = 0, IsActive = true
        };
        var block2 = new ConferencePageBlock
        {
            TenantId = tenant.Id, ConferenceId = source.Id,
            Page = "Home", Culture = "tr-TR",
            BlockType = ConferencePageBlockType.About,
            Title = "Hakkında", ContentJson = "{\"text\":\"lorem\"}",
            Order = 1, IsActive = true
        };
        // Seed: one registration + payment on source (must NOT be cloned)
        var registration = new Registration
        {
            Id = Guid.NewGuid(), AppUserId = "user-1",
            ConferenceId = source.Id, Conference = source,
            RegistrationTypeId = regType1.Id, RegistrationType = regType1,
            Amount = 150
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(), AppUserId = "user-1",
            ConferenceId = source.Id, Conference = source,
            Amount = 150, Currency = "TRY", PaymentMethod = "Receipt"
        };

        await using var ctx = new AppDbContext(options, new TenantContext());
        ctx.AddRange(source, regType1, regType2, topic1, topic2, block1, block2,
            registration, payment);
        await ctx.SaveChangesAsync();

        return (tenant, source);
    }

    /// <summary>
    /// Controller Clone mantığını doğrudan DbContext üzerinde simüle eder.
    /// </summary>
    private static async Task<Conference> RunCloneAsync(
        DbContextOptions<AppDbContext> options,
        Tenant tenant,
        Guid sourceId)
    {
        await using var ctx = new AppDbContext(options, new TenantContext { IsGlobalContext = true });

        var source = await ctx.Conferences.AsNoTracking()
            .FirstAsync(c => c.Id == sourceId);

        var clone = new Conference
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            Title = source.Title + " (Kopya)",
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            IsSubmissionOpen = false,
            IsRegistrationOpen = false,
            Slug = $"kopya-{Guid.NewGuid():N}"
        };
        ctx.Conferences.Add(clone);

        // Topics
        var topics = await ctx.ConferenceTopics.AsNoTracking()
            .Where(t => t.ConferenceId == sourceId).ToListAsync();
        foreach (var t in topics)
            ctx.ConferenceTopics.Add(new ConferenceTopic
            {
                Id = Guid.NewGuid(), ConferenceId = clone.Id,
                Name = t.Name, NameEn = t.NameEn,
                Description = t.Description, DescriptionEn = t.DescriptionEn,
                SortOrder = t.SortOrder, IsActive = t.IsActive
            });

        // RegTypes
        var regTypes = await ctx.RegistrationTypes.AsNoTracking()
            .Where(r => r.ConferenceId == sourceId).ToListAsync();
        foreach (var rt in regTypes)
            ctx.RegistrationTypes.Add(new RegistrationType
            {
                Id = Guid.NewGuid(), ConferenceId = clone.Id,
                Name = rt.Name, NameEn = rt.NameEn,
                Description = rt.Description, DescriptionEn = rt.DescriptionEn,
                Price = rt.Price, Currency = rt.Currency,
                IsActive = rt.IsActive, Deadline = rt.Deadline,
                RoleName = rt.RoleName
            });

        // PageBlocks
        var blocks = await ctx.ConferencePageBlocks.AsNoTracking()
            .Where(b => b.ConferenceId == sourceId).ToListAsync();
        foreach (var b in blocks)
            ctx.ConferencePageBlocks.Add(new ConferencePageBlock
            {
                TenantId = clone.TenantId, ConferenceId = clone.Id,
                Page = b.Page, Culture = b.Culture,
                BlockType = b.BlockType, Title = b.Title,
                Subtitle = b.Subtitle, ContentJson = b.ContentJson,
                Order = b.Order, IsActive = b.IsActive
            });

        await ctx.SaveChangesAsync();
        return clone;
    }

    private static async Task<List<ConferenceTopic>> GetTopicsAsync(
        DbContextOptions<AppDbContext> options, Tenant tenant, Guid confId)
    {
        await using var ctx = new AppDbContext(options, new TenantContext { IsGlobalContext = true });
        return await ctx.ConferenceTopics.Where(t => t.ConferenceId == confId).ToListAsync();
    }

    private static async Task<List<RegistrationType>> GetRegTypesAsync(
        DbContextOptions<AppDbContext> options, Tenant tenant, Guid confId)
    {
        await using var ctx = new AppDbContext(options, new TenantContext { IsGlobalContext = true });
        return await ctx.RegistrationTypes.Where(r => r.ConferenceId == confId).ToListAsync();
    }

    private static async Task<List<ConferencePageBlock>> GetPageBlocksAsync(
        DbContextOptions<AppDbContext> options, Tenant tenant, Guid confId)
    {
        await using var ctx = new AppDbContext(options, new TenantContext { IsGlobalContext = true });
        return await ctx.ConferencePageBlocks.Where(b => b.ConferenceId == confId).ToListAsync();
    }
}
