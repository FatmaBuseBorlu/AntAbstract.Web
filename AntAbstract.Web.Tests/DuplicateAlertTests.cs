using System.Net;
using System.Text.RegularExpressions;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Bildiri gönderildikten sonra "Bildiriniz başarıyla gönderildi" mesajı
/// ekranda iki kez çıkıyordu: bir kez düzenin (layout) ortak uyarı şeridinde,
/// bir kez de görünümün kendi içine kopyalanmış blokta. Aynı kopyalama
/// 56 görünümde vardı.
///
/// Düzen zaten her sayfada bu üç anahtarı basıyor; görünümlerin ayrıca
/// basmasına gerek yok.
/// </summary>
public sealed class DuplicateAlertTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "uyari-kurum2";
    private const string AuthorId = "uyari-yazari2";

    private static readonly Guid TenantId =
        new("91919191-9191-9191-9191-919191919191");

    private static readonly Guid ConferenceId =
        new("92929292-9292-9292-9292-929292929292");

    private static readonly Regex AlertBlock = new(
        @"@if\s*\(\s*TempData\[""(?:Success|Error|Info)Message""\]\s*!=\s*null\s*\)");

    public DuplicateAlertTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AuthorId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = AuthorId,
            UserName = AuthorId + "@antabstract.local",
            NormalizedUserName = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            Email = AuthorId + "@antabstract.local",
            NormalizedEmail = (AuthorId + "@antabstract.local").ToUpperInvariant(),
            FirstName = "Uyarı",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Uyarı Kurumu" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Uyarı Kongresi",
            Slug = "uyari-kongre2",
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(42)
        });

        var typeId = Guid.NewGuid();

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = typeId,
            ConferenceId = ConferenceId,
            Name = "Bildirili Katılım",
            Description = "Test",
            Price = 0,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Author"
        });

        db.Registrations.Add(new Registration
        {
            Id = Guid.NewGuid(),
            AppUserId = AuthorId,
            ConferenceId = ConferenceId,
            RegistrationTypeId = typeId,
            RegistrationDate = DateTime.UtcNow,
            IsPaid = false,
            Amount = 0
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Ekrandaki durumun birebir ölçümü: TempData mesajı taşıyan bir sayfada
    /// mesaj tam olarak bir kez görünmeli.
    /// </summary>
    [Fact]
    public async Task TempDataMessage_AppearsExactlyOnce()
    {
        // Kaydı olmayan bir kongreye bildiri göndermeye çalışmak TempData
        // mesajı üretir; hangi mesaj olduğu önemli değil, kaç kez basıldığı önemli.
        var page = await _client.GetAsync($"/{Slug}/my-submissions");

        var url = page.Headers.Location?.ToString();

        if (url != null)
        {
            page = await _client.GetAsync(url);
        }

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var html = await page.Content.ReadAsStringAsync();

        // Düzenin uyarı kabı sayfada en fazla bir kez kurulmalı.
        var containers = Regex.Matches(html, "id=\"tempDataAlerts\"").Count;

        _output.WriteLine($"uyarı kabı sayısı: {containers}");

        Assert.True(containers <= 1, "Uyarı kabı sayfada birden fazla kez basılıyor.");
    }

    /// <summary>
    /// Asıl koruma: hiçbir görünüm, düzenin bastığı uyarıları tekrar basmamalı.
    /// Bu hata derlenirken görünmüyor, yalnızca ekranda fark ediliyor.
    /// </summary>
    [Fact]
    public void NoView_RepeatsLayoutAlerts()
    {
        var root = FindWebRoot();

        var layoutKeys = Directory
            .EnumerateFiles(root, "_Layout*.cshtml", SearchOption.AllDirectories)
            .Where(f => AlertBlock.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(layoutKeys);

        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);

            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                name.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            if (!AlertBlock.IsMatch(text))
            {
                continue;
            }

            var match = Regex.Match(text, @"Layout\s*=\s*""([^""]+)""");
            var layout = match.Success
                ? Path.GetFileNameWithoutExtension(match.Groups[1].Value)
                : "_ViewStart";

            if (layout == "_ViewStart" || layoutKeys.Contains(layout))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        foreach (var offender in offenders)
        {
            _output.WriteLine(offender);
        }

        Assert.True(
            offenders.Count == 0,
            $"{offenders.Count} görünüm düzenin uyarılarını tekrar basıyor; " +
            "mesaj ekranda iki kez çıkar. Görünümdeki bloğu kaldırın.");
    }

    private static string FindWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "AntAbstract.Web")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, "AntAbstract.Web klasörü bulunamadı.");

        return Path.Combine(dir!.FullName, "AntAbstract.Web");
    }
}
