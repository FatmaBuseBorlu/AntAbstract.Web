using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Dört ekran test ortamında hiç açılamıyordu: sorguları decimal üzerinde
/// ORDER BY / SUM ve TimeSpan üzerinde ORDER BY kullanıyordu. SQL Server
/// bunları çevirebiliyor, SQLite çeviremiyor — yani ekranlar üretimde
/// çalışıyordu ama testte 500 veriyordu ve hiçbir koruma altında değillerdi.
///
/// Sıralama ve toplama artık veritabanının yapabildiği kadarıyla orada,
/// kalanı bellekte yapılıyor. Bu testler ekranların gerçekten açıldığını ve
/// sıranın doğru kaldığını doğruluyor.
/// </summary>
public sealed class SqliteHostileQueryTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    private const string Slug = "sorgu-kurum";

    private static readonly Guid TenantId =
        new("81818181-8181-8181-8181-818181818181");

    private static readonly Guid ConferenceId =
        new("82828282-8282-8282-8282-828282828282");

    public SqliteHostileQueryTests(
        AuthenticatedTestFactory factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = Slug, Name = "Sorgu Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Sorgu Kongresi",
            Slug = "sorgu-kongre",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(32),
            IsRegistrationOpen = true,
            IsSubmissionOpen = true
        });

        // Fiyatlar bilerek karışık sırada: sıralamanın gerçekten çalıştığını
        // görebilmek için.
        AddType(db, "Dinleyici", 400m);
        AddType(db, "Akademisyen", 1000m);
        AddType(db, "Öğrenci", 250m);

        // Aynı gün içinde saatleri karışık sırada eklenen oturumlar.
        AddSession(db, "Öğleden Sonra", new TimeSpan(14, 0, 0), 0);
        AddSession(db, "Sabah", new TimeSpan(9, 0, 0), 0);
        AddSession(db, "Öğle", new TimeSpan(12, 0, 0), 0);

        db.SaveChanges();
    }

    private static void AddType(AppDbContext db, string name, decimal price)
    {
        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = Guid.NewGuid(),
            ConferenceId = ConferenceId,
            Name = name,
            Description = name,
            Price = price,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Listener"
        });
    }

    private static void AddSession(AppDbContext db, string title, TimeSpan start, int order)
    {
        db.Sessions.Add(new Session
        {
            Id = Guid.NewGuid(),
            ConferenceId = ConferenceId,
            Title = title,
            SessionDate = DateTime.Today.AddDays(30),
            StartTime = start,
            EndTime = start.Add(TimeSpan.FromHours(2)),
            SortOrder = order,
            IsActive = true
        });
    }

    /// <summary>
    /// Dördü de eskiden 500 veriyordu; artık açılmalı.
    /// </summary>
    [Theory]
    [InlineData("Kayıt ve Ödemeler", "/Admin/ConferenceFlow/RegistrationsAndPayments")]
    [InlineData("Program / Oturumlar", "/Admin/ConferenceFlow/ProgramSessions")]
    [InlineData("Program Çıktısı", "/Admin/ConferenceFlow/ProgramSessions/Print")]
    public async Task ConferenceScreens_Open(string ad, string path)
    {
        var response = await _client.GetAsync(
            $"/{Slug}{path}?conferenceId={ConferenceId}");

        _output.WriteLine($"{(int)response.StatusCode} {ad}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemReports_Opens()
    {
        var response = await _client.GetAsync("/Admin/SystemReports");

        _output.WriteLine($"{(int)response.StatusCode} Sistem Raporları");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Katılımcının gördüğü kayıt sayfası da aynı decimal sıralamasını
    /// kullanıyordu; ucuzdan pahalıya sıralı gelmeli.
    /// </summary>
    [Fact]
    public async Task RegistrationPage_ListsTypesCheapestFirst()
    {
        var response = await _client.GetAsync($"/{Slug}/registration");

        var url = response.Headers.Location?.ToString() ?? $"/{Slug}/registration";

        if ((int)response.StatusCode is >= 300 and < 400)
        {
            response = await _client.GetAsync(url);
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        var ogrenci = html.IndexOf("renci", StringComparison.Ordinal);
        var dinleyici = html.IndexOf("Dinleyici", StringComparison.Ordinal);
        var akademisyen = html.IndexOf("Akademisyen", StringComparison.Ordinal);

        _output.WriteLine($"Öğrenci {ogrenci}, Dinleyici {dinleyici}, Akademisyen {akademisyen}");

        Assert.True(ogrenci > 0 && dinleyici > 0 && akademisyen > 0,
            "Kayıt türleri sayfada listelenmiyor.");

        Assert.True(ogrenci < dinleyici && dinleyici < akademisyen,
            "Kayıt türleri fiyata göre sıralı değil (250 → 400 → 1000 bekleniyor).");
    }

    /// <summary>Oturumlar gün içinde saate göre sıralı gelmeli.</summary>
    [Fact]
    public async Task ProgramSessions_AreOrderedByStartTime()
    {
        var response = await _client.GetAsync(
            $"/{Slug}/Admin/ConferenceFlow/ProgramSessions?conferenceId={ConferenceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        var sabah = html.IndexOf("Sabah", StringComparison.Ordinal);
        var ogle = html.IndexOf("le</", StringComparison.Ordinal);
        var ogledenSonra = html.IndexOf("leden Sonra", StringComparison.Ordinal);

        _output.WriteLine($"Sabah {sabah}, Öğle {ogle}, Öğleden Sonra {ogledenSonra}");

        Assert.True(sabah > 0, "Oturumlar sayfada listelenmiyor.");
        Assert.True(sabah < ogledenSonra, "Oturumlar saate göre sıralı değil.");
    }
}
