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
/// Kongre sitesi zinciri: site oluşturulur, varsayılan bölümler gelir,
/// bir bölüm düzenlenir ve değişiklik herkese açık sitede görünür.
///
/// E-posta tarafında yalnızca ekranlar ve önizleme kontrol edilir;
/// gönderim kuyruğa atılıp arka planda SMTP ile yollandığı için test
/// sırasında bilerek tetiklenmez.
/// </summary>
public sealed class SiteAndEmailFlowTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _visitor;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "site-akis-kurum";
    private const string ConferenceSlug = "site-akis-kongre";

    private static readonly Guid TenantId = new("12341234-0000-0000-0000-000000000001");
    private static readonly Guid ConferenceId = new("12341234-0000-0000-0000-000000000002");

    public SiteAndEmailFlowTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _admin = factory.CreateClient(new() { AllowAutoRedirect = false });
        _visitor = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Site Akış Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Site Akış Kongresi 2026",
            Slug = ConferenceSlug,
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(42),
            IsRegistrationOpen = true,
            IsSubmissionOpen = true,
            City = "Bursa",
            Country = "Türkiye"
        });

        db.SaveChanges();
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private static async Task<(HttpResponseMessage Response, string Html)> FollowAsync(
        HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var hops = 0;

        while ((int)response.StatusCode is >= 300 and < 400 &&
               response.Headers.Location != null && hops++ < 8)
        {
            response = await client.GetAsync(response.Headers.Location.ToString());
        }

        return (response, System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task SiteOlusturulurDuzenlenirVeZiyaretcideGorunur()
    {
        // 1. Site oluşturma formu açılmalı.
        var form = await FollowAsync(_admin, "/Admin/CentralVitrin/InitSite");

        Assert.Equal(HttpStatusCode.OK, form.Response.StatusCode);

        var created = await _admin.PostAsync(
            "/Admin/CentralVitrin/InitSite",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(form.Html),
                ["conferenceId"] = ConferenceId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        // 2. Bölümler POST'ta değil, yönlendirilen sayfa açılınca oluşuyor.
        var manage = await FollowAsync(
            _admin, $"/Admin/CentralVitrin/ManageBlocks?conferenceId={ConferenceId}");

        Assert.Equal(HttpStatusCode.OK, manage.Response.StatusCode);

        int blockId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var blocks = await db.ConferencePageBlocks.IgnoreQueryFilters()
                .Where(b => b.ConferenceId == ConferenceId)
                .ToListAsync();

            Assert.NotEmpty(blocks);
            _output.WriteLine($"oluşan bölüm: {blocks.Count}");

            blockId = blocks[0].Id;
        }

        // 3. Bölüm düzenlenip kaydedilmeli.
        var edit = await FollowAsync(_admin, $"/Admin/CentralVitrin/EditBlock/{blockId}");

        Assert.Equal(HttpStatusCode.OK, edit.Response.StatusCode);

        var saved = await _admin.PostAsync(
            $"/Admin/CentralVitrin/EditBlock/{blockId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(edit.Html),
                ["Id"] = blockId.ToString(),
                ["ConferenceId"] = ConferenceId.ToString(),
                ["Title"] = "Güncellenmiş Başlık",
                ["Subtitle"] = "Alt açıklama",
                ["IsActive"] = "true"
            }));

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var block = await db.ConferencePageBlocks.IgnoreQueryFilters().FirstAsync(b => b.Id == blockId);

            Assert.Equal("Güncellenmiş Başlık", block.Title);
        }

        // 4. Değişiklik herkese açık sitede görünmeli.
        var site = await FollowAsync(_visitor, $"/{ConferenceSlug}");

        Assert.Equal(HttpStatusCode.OK, site.Response.StatusCode);
        Assert.Contains("Site Akış Kongresi", site.Html, StringComparison.Ordinal);
        Assert.Contains("Güncellenmiş Başlık", site.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Kongre Siteleri", "/Admin/CentralVitrin")]
    [InlineData("Site Bölüm Şablonları", "/Admin/PageBlocks")]
    [InlineData("E-posta Şablonları", "/Admin/EmailTemplates")]
    [InlineData("Gönderim Kaydı", "/Admin/EmailTemplates/SendLog")]
    public async Task SiteVeEpostaEkranlari_Aciliyor(string ad, string path)
    {
        var page = await FollowAsync(_admin, path);

        _output.WriteLine($"{(int)page.Response.StatusCode} {ad}");

        Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);
    }

    /// <summary>Toplu e-posta önizlemesi çalışmalı; gönderim tetiklenmez.</summary>
    [Fact]
    public async Task TopluEposta_OnizlemeCalisiyor()
    {
        var page = await FollowAsync(_admin, $"/{TenantSlug}/Admin/Broadcast");

        Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);

        var preview = await _admin.PostAsync(
            $"/{TenantSlug}/Admin/Broadcast/Preview",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(page.Html),
                ["conferenceId"] = ConferenceId.ToString(),
                ["audience"] = "All",
                ["subject"] = "Test",
                ["body"] = "Deneme"
            }));

        _output.WriteLine($"{(int)preview.StatusCode} önizleme");

        Assert.True((int)preview.StatusCode < 500, "Önizleme hata verdi.");
    }
}
