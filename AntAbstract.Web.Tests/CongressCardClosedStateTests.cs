using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kongre listesinde kayıt alınamayan kartlar.
///
/// İki durum ayrı: tarihi geçmiş kongrede yapılacak bir şey yok, kaydı
/// kapanmış kongrede kayıt sonradan açılabilir. İkisine de aynı yazıyı
/// göstermek kullanıcıyı boşuna bekletiyordu.
/// </summary>
public sealed class CongressCardClosedStateTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    private static readonly Guid TenantId = new("bbbb2222-0000-0000-0000-000000000001");
    private static readonly Guid EndedId = new("bbbb2222-0000-0000-0000-000000000002");
    private static readonly Guid ClosedId = new("bbbb2222-0000-0000-0000-000000000003");

    public CongressCardClosedStateTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = "kapanis-kurum", Name = "Kapanış Üniversitesi" });

        // Tarihi geçmiş kongre.
        db.Conferences.Add(new Conference
        {
            Id = EndedId,
            TenantId = TenantId,
            Title = "Tarihi Geçmiş Kongre",
            Slug = "tarihi-gecmis-kongre",
            StartDate = DateTime.Today.AddDays(-40),
            EndDate = DateTime.Today.AddDays(-38),
            IsRegistrationOpen = true,
            City = "Ankara",
            Country = "Türkiye"
        });

        // Tarihi gelecekte ama kaydı kapalı kongre.
        db.Conferences.Add(new Conference
        {
            Id = ClosedId,
            TenantId = TenantId,
            Title = "Kaydı Kapalı Kongre",
            Slug = "kaydi-kapali-kongre",
            StartDate = DateTime.Today.AddDays(50),
            EndDate = DateTime.Today.AddDays(52),
            IsRegistrationOpen = false,
            City = "İzmir",
            Country = "Türkiye"
        });

        db.SaveChanges();
    }

    private async Task<string> ListingHtmlAsync()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = true });

        // Yonetici ve hakem rollerinde kapali kutu hic basilmiyor; bu ekran
        // katilimci gozuyle dogrulanmali.
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Listener");
        var response = await client.GetAsync("/Home/Congresses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Kart gerçekten listede olmalı; yoksa aşağıdaki testler boşa geçer.</summary>
    [Fact]
    public async Task KayitAlinamayanKartlar_ListedeGorunuyor()
    {
        var html = await ListingHtmlAsync();

        Assert.Contains("Tarihi Geçmiş Kongre", html, StringComparison.Ordinal);
        Assert.Contains("Kaydı Kapalı Kongre", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TarihiGecmisKongre_SonaErdiYazar()
    {
        var html = await ListingHtmlAsync();
        var card = CardFor(html, "Tarihi Geçmiş Kongre");

        _output.WriteLine(card);

        // Test ortami en-US calisiyor; metin yerine dile bagli olmayan
        // ikonla, ayrimi ise iki dildeki karsiliklarla dogruluyoruz.
        Assert.Contains("fa-flag-checkered", card, StringComparison.Ordinal);
        Assert.True(
            card.Contains("Sona Erdi", StringComparison.Ordinal) ||
            card.Contains("Ended", StringComparison.Ordinal),
            "Tarihi geçmiş kongrede bitiş yazısı görünmüyor.");
        Assert.DoesNotContain("fa-lock", card, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KaydiKapaliKongre_KayitKapaliYazar()
    {
        var html = await ListingHtmlAsync();
        var card = CardFor(html, "Kaydı Kapalı Kongre");

        _output.WriteLine(card);

        Assert.Contains("fa-lock", card, StringComparison.Ordinal);
        Assert.True(
            card.Contains("Kayıt Kapalı", StringComparison.Ordinal) ||
            card.Contains("Registration Closed", StringComparison.Ordinal),
            "Kaydı kapalı kongrede kapalı yazısı görünmüyor.");
        Assert.DoesNotContain("fa-flag-checkered", card, StringComparison.Ordinal);
    }

    /// <summary>
    /// Anahtarın karşılığı yoksa Razor anahtar adını basar ve kullanıcı
    /// ekranda "RegistrationClosedShort" görür — bu daha önce yaşandı.
    /// </summary>
    [Fact]
    public async Task CeviriAnahtarlari_HamHalleriyleBasilmiyor()
    {
        var html = await ListingHtmlAsync();

        Assert.DoesNotContain("RegistrationClosedShort", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ConferenceEndedShort", html, StringComparison.Ordinal);
    }

    /// <summary>Kapalı kutu artık soluk gri değil, amber tonunda.</summary>
    [Fact]
    public async Task KapaliKutu_AmberTonunda()
    {
        var html = await ListingHtmlAsync();

        Assert.Contains("rgba(245, 158, 11, 0.10)", html, StringComparison.Ordinal);
        Assert.Contains("#b45309", html, StringComparison.Ordinal);
    }

    /// <summary>Kartın gövdesini ayıklar; iddialar komşu karta kaymasın.</summary>
    private static string CardFor(string html, string title)
    {
        var titleIndex = html.IndexOf(title, StringComparison.Ordinal);

        Assert.True(titleIndex > 0, $"'{title}' kartı listede bulunamadı.");

        var start = html.LastIndexOf("<article", titleIndex, StringComparison.Ordinal);
        var end = html.IndexOf("</article>", titleIndex, StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, $"'{title}' kartının sınırları bulunamadı.");

        return html[start..end];
    }
}
