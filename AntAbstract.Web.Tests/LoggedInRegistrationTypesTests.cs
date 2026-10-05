using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Canlıda UBBK için türler tanımlıyken giriş yapmış bir katılımcı
/// "Aktif başvuru türü bulunamadı" gördü. Anonim ziyaretçi türleri görüyor;
/// burada giriş yapmış, kaydı olmayan kullanıcının izlediği yollar
/// (kurum adresi, kongre adresi, özet gönder yönlendirmesi, oturumda başka
/// kongre seçiliyken) deneniyor.
/// </summary>
public sealed class LoggedInRegistrationTypesTests : IClassFixture<AuthenticatedTestFactory>
{
    private const string TenantSlug = "ubbk-test";
    private const string ConferenceSlug = "9-ulusal-ubbk-test";
    private const string OldConferenceSlug = "8-ulusal-ubbk-test";
    private const string UserId = "ubbk-katilimci";

    private static readonly Guid TenantId = new("d1d1d1d1-0000-0000-0000-0000000000d1");
    private static readonly Guid ConferenceId = new("d2d2d2d2-0000-0000-0000-0000000000d2");
    private static readonly Guid OldConferenceId = new("d2d2d2d2-0000-0000-0000-0000000000e2");

    private readonly AuthenticatedTestFactory _factory;

    public LoggedInRegistrationTypesTests(AuthenticatedTestFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
            return;

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "UBBK Test" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "9. UBBK Test",
            Slug = ConferenceSlug,
            StartDate = new DateTime(2026, 12, 16),
            EndDate = new DateTime(2026, 12, 17)
        });

        // Aynı kurumun geçmiş kongresi, türlerinin süresi dolmuş.
        db.Conferences.Add(new Conference
        {
            Id = OldConferenceId,
            TenantId = TenantId,
            Title = "8. UBBK Test",
            Slug = OldConferenceSlug,
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(41)
        });

        foreach (var (conf, name, deadline) in new[]
                 {
                     (ConferenceId, "Dinleyici Early Bird", new DateTime(2026, 12, 15)),
                     (ConferenceId, "Yazar Katılım Ücreti Early Bird", new DateTime(2026, 12, 15)),
                     (OldConferenceId, "Eski Tür", DateTime.Today.AddDays(-1))
                 })
        {
            db.RegistrationTypes.Add(new RegistrationType
            {
                Id = Guid.NewGuid(),
                ConferenceId = conf,
                Name = name,
                Description = "Test",
                Price = 800,
                Currency = "TRY",
                IsActive = true,
                Deadline = deadline,
                RoleName = "Author"
            });
        }

        db.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = $"{UserId}@antabstract.local",
            NormalizedUserName = $"{UserId}@ANTABSTRACT.LOCAL",
            Email = $"{UserId}@antabstract.local",
            NormalizedEmail = $"{UserId}@ANTABSTRACT.LOCAL",
            FirstName = "Katılımcı",
            LastName = "Test",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.SaveChanges();
    }

    private HttpClient Participant()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, UserId);
        return client;
    }

    private static async Task<string> Body(HttpResponseMessage r) =>
        WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());

    [Theory]
    [InlineData("/" + ConferenceSlug + "/registration")]
    [InlineData("/" + TenantSlug + "/registration")]
    [InlineData("/" + ConferenceSlug + "/submit-abstract")]
    public async Task LoggedInParticipant_SeesTypes(string url)
    {
        var html = await Body(await Participant().GetAsync(url));

        Assert.DoesNotContain("Aktif başvuru türü bulunamadı", html);
        Assert.Contains("Dinleyici Early Bird", html);
    }

    [Fact]
    public async Task LoggedInParticipant_AfterVisitingOtherConferenceOfSameTenant_SeesTypes()
    {
        var client = Participant();

        // Önce aynı kurumun diğer kongresine bakar (oturuma o kongre yazılır)...
        await client.GetAsync($"/{OldConferenceSlug}/registration");

        // ...sonra kongre sitesindeki kurum adresli butonla UBBK'ya gelir.
        var html = await Body(await client.GetAsync($"/{TenantSlug}/registration"));

        Assert.Contains("9. UBBK Test", html);
        Assert.DoesNotContain("Aktif başvuru türü bulunamadı", html);
    }

    [Fact]
    public async Task ConferenceSite_RegisterButton_UsesConferenceAddress()
    {
        var html = await Body(await Participant().GetAsync($"/{ConferenceSlug}"));

        Assert.Contains($"/{ConferenceSlug}/registration", html);
        Assert.DoesNotContain($"/{TenantSlug}/registration", html);
    }
}
