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
/// Özet gönderme formunda konu seçimi zorunlu ve liste kongreye tanımlı aktif
/// konulardan doluyor. Hiç konu yoksa liste boş kalıyor, alan zorunlu olduğu
/// için bildiri hiç gönderilemiyor.
///
/// Bu ekran TenantAdminOnly politikasındaydı ve SuperAdmin'i dışlıyordu;
/// SuperAdmin hiçbir kuruma bağlı olmadığı için konu hiç eklenemiyordu.
/// </summary>
public sealed class SuperAdminConferenceTopicTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string Slug = "konu-kurum";

    private static readonly Guid TenantId =
        new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static readonly Guid ConferenceId =
        new("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public SuperAdminConferenceTopicTests(
        AuthenticatedTestFactory factory, ITestOutputHelper output)
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

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Konu Kurumu" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Konu Kongresi",
            Slug = "konu-kongresi",
            StartDate = DateTime.Today.AddDays(45),
            EndDate = DateTime.Today.AddDays(47)
        });

        db.SaveChanges();
    }

    [Fact]
    public async Task SelectConferencePage_Opens()
    {
        var response = await _client.GetAsync("/Admin/ConferenceTopics");

        _output.WriteLine($"{(int)response.StatusCode} kongre seçme ekranı");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(ConferenceId.ToString(), html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Konu eklenebilmeli ve eklenen konu özet gönderme formundaki listede
    /// gerçekten görünmeli — asıl amaç bu.
    /// </summary>
    [Fact]
    public async Task Topic_CanBeCreated_AndBecomesSelectable()
    {
        var selectPage = await _client.GetAsync("/Admin/ConferenceTopics");
        var selectToken = Regex.Match(
            await selectPage.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var selected = await _client.PostAsync(
            "/Admin/ConferenceTopics/Select",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = selectToken,
                ["conferenceId"] = ConferenceId.ToString()
            }));

        _output.WriteLine($"{(int)selected.StatusCode} kongre seçimi");
        Assert.Equal(HttpStatusCode.Redirect, selected.StatusCode);

        var form = await _client.GetAsync($"/{Slug}/Admin/ConferenceTopics/Create");

        _output.WriteLine($"{(int)form.StatusCode} ekleme formu");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);

        var token = Regex.Match(
            await form.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var save = await _client.PostAsync(
            $"/{Slug}/Admin/ConferenceTopics/Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ConferenceId"] = ConferenceId.ToString(),
                ["Slug"] = Slug,
                ["Name"] = "Yapay Zekâ",
                ["NameEn"] = "Artificial Intelligence",
                ["Description"] = "Makine öğrenmesi ve derin öğrenme",
                ["IsActive"] = "true",
                ["SortOrder"] = "1"
            }));

        _output.WriteLine($"{(int)save.StatusCode} konu kaydetme");
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var topic = await db.ConferenceTopics
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ConferenceId == ConferenceId && t.Name == "Yapay Zekâ");

        Assert.True(topic != null, "Konu oluşturulmadı.");

        // Özet formundaki liste tam olarak bu koşulla doluyor:
        // aynı kongre + IsActive. Konu bu filtreden geçmezse alan boş kalır
        // ve zorunlu olduğu için bildiri gönderilemez.
        Assert.True(topic!.IsActive, "Konu aktif değil; özet formunda görünmez.");

        var selectable = await db.ConferenceTopics
            .IgnoreQueryFilters()
            .Where(t => t.ConferenceId == ConferenceId && t.IsActive)
            .CountAsync();

        _output.WriteLine($"özet formunda seçilebilir konu sayısı: {selectable}");

        Assert.True(selectable > 0, "Özet gönderme formunda seçilebilir konu yok.");
    }
}
