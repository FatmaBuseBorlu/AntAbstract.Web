using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kayıt Türleri ve Bildiri Konuları menüde yalnızca kongre seçiliyken
/// görünüyordu. Panele yeni girildiğinde seçim henüz yapılmadığı için
/// ekranlar menüde hiç yer almıyor, kullanıcı varlıklarını fark edemiyordu.
/// Kayıt türü tanımlanmamış bir kongreye kimse kaydolamıyor; bu yüzden
/// ekranın bulunabilir olması akışın ön koşulu.
/// </summary>
public sealed class MenuDiscoverabilityTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly Guid TenantId =
        new("d5d5d5d5-d5d5-d5d5-d5d5-d5d5d5d5d5d5");

    public MenuDiscoverabilityTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = "menu-bulunabilir", Name = "Menü Kurumu" });

        db.Conferences.Add(new Conference
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Title = "Menü Kongresi",
            Slug = "menu-bulunabilir-kongre",
            StartDate = DateTime.Today.AddDays(20),
            EndDate = DateTime.Today.AddDays(22)
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Kongre seçilmemişken bile iki bağlantı menüde olmalı; ikisi de
    /// kongre seçme adımıyla başlayan adrese gider.
    /// </summary>
    [Theory]
    [InlineData("Kayıt Türleri", "/Admin/RegistrationTypes")]
    [InlineData("Bildiri Konuları", "/Admin/ConferenceTopics")]
    public async Task Menu_ShowsSetupLinks_WithoutSelectedConference(string ad, string href)
    {
        var response = await _client.GetAsync("/Dashboard/SuperAdmin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        var found = html.Contains($"href=\"{href}\"", StringComparison.Ordinal);

        _output.WriteLine($"{ad}: {found}");

        Assert.True(found, $"{ad} kongre seçilmemişken menüde görünmüyor.");
    }

    /// <summary>Bağlantı gerçekten kongre seçme ekranını açmalı.</summary>
    [Fact]
    public async Task SetupLink_OpensConferencePicker()
    {
        var response = await _client.GetAsync("/Admin/RegistrationTypes");

        _output.WriteLine($"{(int)response.StatusCode} /Admin/RegistrationTypes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("conference-select-card", html, StringComparison.Ordinal);
    }
}
