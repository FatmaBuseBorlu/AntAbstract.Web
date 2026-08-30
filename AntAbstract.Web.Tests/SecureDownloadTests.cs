using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Yüklenen bildiri dosyasının indirilmesi.
///
/// İndirme adresleri (/download/...) slug taşımıyor. Slug yoksa tenant
/// bağlamı boş kalıyor ve global erişim yalnızca SuperAdmin için açılıyor;
/// bu yüzden çok kiracılı sorgu filtresi normal bir yazar için SubmissionFiles
/// ve Registrations tablolarındaki her satırı eliyordu. Sonuç: kullanıcı kendi
/// yüklediği dosyayı indiremiyor, ekrana 404 geliyordu.
/// </summary>
public sealed class SecureDownloadTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _author;
    private readonly ITestOutputHelper _output;

    private const string AuthorId = "indirme-yazar";
    private const string OutsiderId = "indirme-yabanci";
    private const string FileContent = "BILDIRI ICERIGI";

    private static readonly Guid TenantId = new("77778888-1111-2222-3333-444455556666");
    private static readonly Guid ConferenceId = new("77778888-1111-2222-3333-444455556667");
    private static readonly Guid SubmissionId = new("77778888-1111-2222-3333-444455556668");
    private static readonly Guid RegistrationId = new("77778888-1111-2222-3333-444455556669");
    private static readonly Guid RegistrationTypeId = new("77778888-1111-2222-3333-44445555666a");

    private const int FileId = 910001;

    private readonly AuthenticatedTestFactory _factory;

    public SecureDownloadTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        _author = factory.CreateClient(new() { AllowAutoRedirect = false });
        _author.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _author.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AuthorId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.SubmissionFiles.IgnoreQueryFilters().Any(f => f.Id == FileId))
        {
            return;
        }

        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        // Dosyanın diskte gerçekten bulunması gerekiyor; yoksa satır bulunsa
        // bile 404 döner ve test hangi sebepten olduğunu ayırt edemez.
        var folder = Path.Combine(env.ContentRootPath, "private-uploads", "submissions");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "indirme-testi.pdf"), FileContent);

        foreach (var (id, ad) in new[] { (AuthorId, "Yazar"), (OutsiderId, "Yabancı") })
        {
            db.Users.Add(new AppUser
            {
                Id = id,
                UserName = $"{id}@test.local",
                NormalizedUserName = $"{id}@TEST.LOCAL".ToUpperInvariant(),
                Email = $"{id}@test.local",
                NormalizedEmail = $"{id}@TEST.LOCAL".ToUpperInvariant(),
                FirstName = ad,
                LastName = "Test",
                SecurityStamp = Guid.NewGuid().ToString()
            });
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = "indirme-kurum", Name = "İndirme Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "İndirme Kongresi 2026",
            Slug = "indirme-kongre",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32),
            IsBlindReview = false,
            City = "Ankara",
            Country = "Türkiye"
        });

        db.Submissions.Add(new Submission
        {
            Id = SubmissionId,
            TenantId = TenantId,
            ConferenceId = ConferenceId,
            AuthorId = AuthorId,
            Title = "İndirme Testi Bildirisi",
            Abstract = "Özet",
            CreatedAt = DateTime.UtcNow
        });

        db.SubmissionFiles.Add(new SubmissionFile
        {
            Id = FileId,
            SubmissionId = SubmissionId,
            FileName = "indirme-testi.pdf",
            StoredFileName = "indirme-testi.pdf",
            FilePath = "private-uploads/submissions/indirme-testi.pdf",
            Version = 1,
            UploadedAt = DateTime.UtcNow
        });

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = RegistrationTypeId,
            ConferenceId = ConferenceId,
            Name = "Bildirili Katılım",
            Price = 100m,
            IsActive = true
        });

        db.Registrations.Add(new Registration
        {
            Id = RegistrationId,
            ConferenceId = ConferenceId,
            RegistrationTypeId = RegistrationTypeId,
            AppUserId = AuthorId,
            RegistrationDate = DateTime.UtcNow,
            ReceiptFilePath = "private-uploads/submissions/indirme-testi.pdf"
        });

        db.SaveChanges();
    }

    [Fact]
    public async Task Yazar_KendiYukledigiDosyayiIndirebilir()
    {
        var response = await _author.GetAsync($"/download/submission/{FileId}");

        _output.WriteLine($"{(int)response.StatusCode} bildiri dosyası");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FileContent, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Yazar_KendiMakbuzunuIndirebilir()
    {
        var response = await _author.GetAsync($"/download/receipt/{RegistrationId}");

        _output.WriteLine($"{(int)response.StatusCode} makbuz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Düzeltme yetkiyi gevşetmemeli: başkasının dosyası indirilememeli.</summary>
    [Fact]
    public async Task Yabanci_BaskasininDosyasiniIndiremez()
    {
        var outsider = _factory.CreateClient(new() { AllowAutoRedirect = false });
        outsider.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        outsider.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, OutsiderId);

        var response = await outsider.GetAsync($"/download/submission/{FileId}");

        _output.WriteLine($"{(int)response.StatusCode} yabancı erişimi");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
