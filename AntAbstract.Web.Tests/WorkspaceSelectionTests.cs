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
/// "Çalışma Alanı Seçimi" ekranındaki "Seç ve Başla" düğmesi.
///
/// Bu ekran slug taşımayan global bir adreste (/Dashboard/...) çalışıyor.
/// Orada TenantContext.Current null kalıyor ve IsGlobalContext yalnızca
/// SuperAdmin için açılıyor; dolayısıyla çok kiracılı sorgu filtresi normal
/// bir yazar için Conferences tablosundaki her satırı eliyor.
///
/// Listeleme tarafı bunu bildiği için IgnoreQueryFilters kullanıyordu, ama
/// seçme işlemi kullanmıyordu: kart ekranda görünüyor, tıklanınca "Kongre
/// bulunamadı." hatası veriyordu — kongre dururken.
/// </summary>
public sealed class WorkspaceSelectionTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string UserId = "calisma-alani-yazar";

    private static readonly Guid TenantId = new("55556666-1111-2222-3333-444455556666");
    private static readonly Guid ConferenceId = new("55556666-1111-2222-3333-444455556667");
    private static readonly Guid RegistrationId = new("55556666-1111-2222-3333-444455556668");
    private static readonly Guid RegistrationTypeId = new("55556666-1111-2222-3333-444455556669");

    public WorkspaceSelectionTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, UserId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "calisma.alani@test.local",
            NormalizedUserName = "CALISMA.ALANI@TEST.LOCAL",
            Email = "calisma.alani@test.local",
            NormalizedEmail = "CALISMA.ALANI@TEST.LOCAL",
            FirstName = "Fatma",
            LastName = "Borlu",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = "calisma-alani-kurum",
            Name = "Çalışma Alanı Üniversitesi"
        });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Çalışma Alanı Kongresi 2026",
            Slug = "calisma-alani-kongre",
            StartDate = DateTime.Today.AddDays(60),
            EndDate = DateTime.Today.AddDays(62),
            IsRegistrationOpen = true,
            IsSubmissionOpen = true,
            City = "Konya",
            Country = "Türkiye"
        });

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = RegistrationTypeId,
            ConferenceId = ConferenceId,
            Name = "Bildirili Katılım",
            Price = 100m,
            IsActive = true
        });

        // Kullanıcı bu kongreye gerçekten kayıtlı — kart listede çıkıyor.
        db.Registrations.Add(new Registration
        {
            Id = RegistrationId,
            ConferenceId = ConferenceId,
            RegistrationTypeId = RegistrationTypeId,
            AppUserId = UserId,
            RegistrationDate = DateTime.UtcNow
        });

        db.SaveChanges();
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    [Fact]
    public async Task SecVeBasla_PaneleGoturur_HataVermez()
    {
        // Kart listede görünüyor mu?
        var list = await _client.GetAsync("/Dashboard/MyConferences");
        var listHtml = WebUtility.HtmlDecode(await list.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("Çalışma Alanı Kongresi 2026", listHtml, StringComparison.Ordinal);

        // Kart görünüyorsa seçilebilmeli.
        var selected = await _client.PostAsync(
            "/Dashboard/SelectConferencePost",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(listHtml),
                ["conferenceId"] = ConferenceId.ToString()
            }));

        var target = selected.Headers.Location?.ToString() ?? "";

        _output.WriteLine($"{(int)selected.StatusCode} -> {target}");

        Assert.Equal(HttpStatusCode.Redirect, selected.StatusCode);

        // Hata olduğunda kod MyConferences'a geri atıyor. Panele gitmeli.
        Assert.DoesNotContain("MyConferences", target, StringComparison.OrdinalIgnoreCase);

        // Yönlendirilen sayfada hata mesajı çıkmamalı.
        var landing = await _client.GetAsync(target);
        var landingHtml = WebUtility.HtmlDecode(await landing.Content.ReadAsStringAsync());

        Assert.DoesNotContain("Kongre bulunamadı", landingHtml, StringComparison.Ordinal);
    }
}
