using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kullanıcı kongreye kaydolduğu hâlde "Kongrelerim" ekranında hiçbir kongre
/// görünmüyordu. Bu ekranın adresinde kurum slug'ı yok; global kurum bağlamı
/// yalnızca SuperAdmin için açıldığından, normal kullanıcıda kurum filtresi
/// tüm satırları eliyor olabilir.
/// </summary>
public sealed class MyConferencesVisibilityTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string AuthorId = "kongrelerim-yazari";
    private const string ConferenceTitle = "Kongrelerimde Görünmeli";

    private static readonly Guid TenantId =
        new("9a9a9a9a-9a9a-9a9a-9a9a-9a9a9a9a9a9a");

    private static readonly Guid ConferenceId =
        new("9b9b9b9b-9b9b-9b9b-9b9b-9b9b9b9b9b9b");

    public MyConferencesVisibilityTests(
        AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        _client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AuthorId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.Any(t => t.Id == TenantId))
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
            FirstName = "Kongrelerim",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = "kongrelerim-kurum",
            Name = "Kongrelerim Kurumu"
        });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = ConferenceTitle,
            Slug = "kongrelerim-kongresi",
            StartDate = DateTime.Today.AddDays(15),
            EndDate = DateTime.Today.AddDays(17)
        });

        var typeId = Guid.NewGuid();

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = typeId,
            ConferenceId = ConferenceId,
            Name = "Akademisyen",
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

    [Fact]
    public async Task RegisteredConference_AppearsOnMyConferences()
    {
        var response = await _client.GetAsync("/Dashboard/MyConferences");

        _output.WriteLine($"{(int)response.StatusCode} /Dashboard/MyConferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var decoded = System.Net.WebUtility.HtmlDecode(html);

        _output.WriteLine($"sayfada kongre adı var mı: {decoded.Contains(ConferenceTitle)}");

        Assert.Contains(ConferenceTitle, decoded);
    }
}
