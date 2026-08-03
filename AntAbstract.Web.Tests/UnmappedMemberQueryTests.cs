using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Submission.User / Submission.UserId gibi [NotMapped] kısayollar sorguda
/// kullanılırsa EF Core çeviremez ve sayfa 500 verir. Bu tür sayfaları
/// gerçek veriyle çağırarak kırılmayı yakalar.
/// </summary>
public sealed class UnmappedMemberQueryTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly Guid TenantId =
        new("33333333-3333-3333-3333-333333333333");

    private static readonly Guid ConferenceId =
        new("44444444-4444-4444-4444-444444444444");

    private const string UserId = "unmapped-test-user";

    public UnmappedMemberQueryTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (db.Users.Any(u => u.Id == UserId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "unmapped@test.local",
            NormalizedUserName = "UNMAPPED@TEST.LOCAL",
            Email = "unmapped@test.local",
            NormalizedEmail = "UNMAPPED@TEST.LOCAL",
            FirstName = "Test",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = "unmapped-kurum",
            Name = "Unmapped Kurum"
        });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Unmapped Kongre",
            Slug = "unmapped-kongre",
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(12)
        });

        db.SaveChanges();
    }

    /// <summary>
    /// Kullanıcı detayı: bildiri sayısı Submission.UserId ([NotMapped])
    /// üzerinden sayılırsa sorgu çevrilemez.
    /// </summary>
    [Fact]
    public async Task UserDetails_Opens()
    {
        var response = await _client.GetAsync($"/Admin/Users/Details/{UserId}");

        _output.WriteLine($"{(int)response.StatusCode} kullanıcı detayı");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserDetails_DoesNotThrow_ForUnknownUser()
    {
        var response = await _client.GetAsync("/Admin/Users/Details/yok-boyle-kullanici");

        _output.WriteLine($"{(int)response.StatusCode} bilinmeyen kullanıcı");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Toplu e-posta alıcı sayısı: hedef gruplardan üçü Submission.User
    /// ([NotMapped]) üzerinden sorgulanıyordu.
    /// </summary>
    [Theory]
    [InlineData("accepted")]
    [InlineData("revision")]
    [InlineData("reviewers")]
    [InlineData("registered")]
    [InlineData("paid")]
    [InlineData("unpaid")]
    [InlineData("all")]
    public async Task BroadcastPage_DoesNotThrow_ForEachTargetGroup(string group)
    {
        var response = await _client.GetAsync(
            $"/Admin/Broadcast?conferenceId={ConferenceId}&group={group}");

        _output.WriteLine($"{(int)response.StatusCode} toplu e-posta [{group}]");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task CertificatesPage_DoesNotThrow()
    {
        var response = await _client.GetAsync(
            $"/Admin/Certificates?conferenceId={ConferenceId}");

        _output.WriteLine($"{(int)response.StatusCode} sertifikalar");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
