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
/// Kurum bağlamlı (/{slug}/Admin/...) ekleme formları.
///
/// Bu entity'lerin Conference navigasyonu nullable değil; MVC onu zorunlu
/// sayıyor ama form göndermiyor. ModelState temizlenmezse doğrulama hiçbir
/// zaman geçmiyor ve kayıt sessizce oluşmuyor.
/// </summary>
public sealed class TenantScopedCreateTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "kurum-kapsamli-test";

    private static readonly Guid TenantId =
        new("77777777-7777-7777-7777-777777777777");

    private static readonly Guid ConferenceId =
        new("88888888-8888-8888-8888-888888888888");

    public TenantScopedCreateTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = Slug,
            Name = "Kurum Kapsamlı Test"
        });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Kurum Kapsamlı Kongre",
            Slug = "kurum-kapsamli-kongre",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32)
        });

        db.SaveChanges();
    }

    private async Task<string?> TokenFromAsync(string url)
    {
        var page = await _client.GetAsync(url);

        if (page.StatusCode != HttpStatusCode.OK)
        {
            _output.WriteLine($"{(int)page.StatusCode} {url} — form açılmadı");
            return null;
        }

        var html = await page.Content.ReadAsStringAsync();

        var m = Regex.Match(
            html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        return m.Success ? m.Groups[1].Value : null;
    }

    [Fact]
    public async Task Speaker_CanBeCreated()
    {
        var url = $"/{Slug}/Admin/Speakers/Create?conferenceId={ConferenceId}";
        var token = await TokenFromAsync(url);

        Assert.NotNull(token);

        var response = await _client.PostAsync(
            $"/{Slug}/Admin/Speakers/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token!,
                ["ConferenceId"] = ConferenceId.ToString(),
                ["FullName"] = "Prof. Dr. Test Konuşmacı",
                ["Title"] = "Profesör",
                ["Institution"] = "Test Üniversitesi"
            }));

        _output.WriteLine($"{(int)response.StatusCode} konuşmacı ekleme");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(
            await db.InvitedSpeakers.IgnoreQueryFilters()
                .AnyAsync(s => s.ConferenceId == ConferenceId &&
                               s.FullName == "Prof. Dr. Test Konuşmacı"),
            "Konuşmacı kaydedilmedi.");
    }

    [Fact]
    public async Task Sponsor_CanBeCreated()
    {
        var url = $"/{Slug}/Admin/Sponsors/Create?conferenceId={ConferenceId}";
        var token = await TokenFromAsync(url);

        Assert.NotNull(token);

        var response = await _client.PostAsync(
            $"/{Slug}/Admin/Sponsors/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token!,
                ["ConferenceId"] = ConferenceId.ToString(),
                ["Name"] = "Test Sponsor A.Ş.",
                ["Tier"] = "Gold"
            }));

        _output.WriteLine($"{(int)response.StatusCode} sponsor ekleme");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(
            await db.Sponsors.IgnoreQueryFilters()
                .AnyAsync(s => s.ConferenceId == ConferenceId &&
                               s.Name == "Test Sponsor A.Ş."),
            "Sponsor kaydedilmedi.");
    }
}
