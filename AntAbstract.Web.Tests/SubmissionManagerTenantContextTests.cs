using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Hakem tarafında kanıtlanan hatanın yazar tarafında da olup olmadığı.
///
/// SubmissionManager'ın sorguları kiracı filtresinden muaf değil. Yazar
/// ekranları slug taşımayan adreslerden de açılabiliyor; orada bağlam boş
/// kalırsa bildiri bulunamaz ve yazar kendi bildirisini göremez veya
/// düzenleyemez.
///
/// Deney HTTP katmanını dışarıda tutuyor: tek değişken kiracı bağlamı.
/// Aynı çağrı iki bağlamda çalıştırılıp sonuç karşılaştırılıyor; "boş liste"
/// tek başına kanıt değil, global bağlam doluyken boş bağlamın boş olması
/// kesin olarak bu hatadır.
/// </summary>
public sealed class SubmissionManagerTenantContextTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    private readonly string _authorId = "sm-yazar-" + Guid.NewGuid().ToString("N")[..8];
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _conferenceId = Guid.NewGuid();
    private readonly Guid _submissionId = Guid.NewGuid();

    public SubmissionManagerTenantContextTests(
        AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Add(new AppUser
        {
            Id = _authorId,
            UserName = _authorId + "@test.local",
            NormalizedUserName = (_authorId + "@TEST.LOCAL").ToUpperInvariant(),
            Email = _authorId + "@test.local",
            NormalizedEmail = (_authorId + "@TEST.LOCAL").ToUpperInvariant(),
            FirstName = "Yazar",
            LastName = "Testi",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Slug = "sm-kurum-" + _tenantId.ToString("N")[..8],
            Name = "SM Üniversitesi"
        });

        db.Conferences.Add(new Conference
        {
            Id = _conferenceId,
            TenantId = _tenantId,
            Title = "SM Kongresi 2026",
            Slug = "sm-kongre-" + _conferenceId.ToString("N")[..8],
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32),
            IsSubmissionOpen = true,
            City = "Ankara",
            Country = "Türkiye"
        });

        db.Submissions.Add(new Submission
        {
            Id = _submissionId,
            TenantId = _tenantId,
            ConferenceId = _conferenceId,
            AuthorId = _authorId,
            Title = "Bağlam Testi Bildirisi",
            Abstract = "Özet",
            Keywords = "test",
            Topic = string.Empty,
            PresentationType = "Sözlü Sunum",
            Status = SubmissionStatus.New,
            CreatedDate = DateTime.UtcNow
        });

        db.SaveChanges();
    }

    private async Task<T> WithContextAsync<T>(bool global, Func<ISubmissionService, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<TenantContext>().IsGlobalContext = global;

        return await work(scope.ServiceProvider.GetRequiredService<ISubmissionService>());
    }

    [Fact]
    public async Task KendiBildirileri_BosBaglamdaDaGoruluyor()
    {
        var global = await WithContextAsync(true, s => s.GetMySubmissionsAsync(_authorId));
        var bos = await WithContextAsync(false, s => s.GetMySubmissionsAsync(_authorId));

        _output.WriteLine($"global: {global.Count}  boş bağlam: {bos.Count}");

        Assert.NotEmpty(global);

        Assert.True(
            bos.Count == global.Count,
            $"Yazarın bildiri listesi boş bağlamda {bos.Count}, global bağlamda " +
            $"{global.Count}. Slug taşımayan adreste yazar bildirilerini göremez.");
    }

    [Fact]
    public async Task BildiriDetayi_BosBaglamdaDaBulunuyor()
    {
        var global = await WithContextAsync(true, s => s.GetSubmissionByIdAsync(_submissionId));
        var bos = await WithContextAsync(false, s => s.GetSubmissionByIdAsync(_submissionId));

        _output.WriteLine($"global: {global != null}  boş bağlam: {bos != null}");

        Assert.NotNull(global);

        Assert.NotNull(bos);
    }

    // GetActiveConferencesAsync ve GetAllSubmissionsAsync bu sürümde hiçbir
    // yerden çağrılmıyor (ölü kod), bu yüzden bağlam testi kapsamına
    // alınmadı. GetAllSubmissionsAsync ayrıca kapsam parametresi almıyor;
    // orada filtre muafiyeti kurumlar arası sızıntı olurdu.
}
