using System.Net;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kayıt başarı sayfasındaki "Bildiri Gönderimine Geç" bağlantısı kurum slug'ı
/// taşıyor (kanonik slug kurum slug'ı olarak üretiliyor). Kurumda birden fazla
/// kongre varsa, kurum slug'ından kongre çözümlemesi en yeni tarihliyi seçiyor
/// olabilir — bu da kullanıcıyı kaydolmadığı bir kongreye götürür ve
/// "önce kayıt olun" diyerek geri atar.
/// </summary>
public sealed class SubmitAbstractRedirectTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "coklu-kongre-kurum";
    private const string RegisteredConfSlug = "kaydoldugum-kongre";
    private const string AuthorId = "coklu-kongre-yazari";

    private static readonly Guid TenantId =
        new("11111111-2222-3333-4444-555555555555");

    // Yazarın kaydolduğu kongre — başlangıcı DAHA ERKEN
    private static readonly Guid RegisteredConferenceId =
        new("66666666-7777-8888-9999-000000000001");

    // Aynı kurumda başlangıcı DAHA GEÇ olan başka bir kongre
    private static readonly Guid OtherConferenceId =
        new("66666666-7777-8888-9999-000000000002");

    private static readonly Guid RegistrationId =
        new("66666666-7777-8888-9999-0000000000aa");

    public SubmitAbstractRedirectTests(
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
            FirstName = "Çoklu",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Slug = TenantSlug,
            Name = "Çoklu Kongre Kurumu"
        });

        db.Conferences.Add(new Conference
        {
            Id = RegisteredConferenceId,
            TenantId = TenantId,
            Title = "Kaydolduğum Kongre",
            Slug = RegisteredConfSlug,
            StartDate = DateTime.Today.AddDays(10),
            EndDate = DateTime.Today.AddDays(12),
            IsSubmissionOpen = true
        });

        db.Conferences.Add(new Conference
        {
            Id = OtherConferenceId,
            TenantId = TenantId,
            Title = "Başka Kongre",
            Slug = "baska-kongre",
            StartDate = DateTime.Today.AddDays(300),
            EndDate = DateTime.Today.AddDays(302),
            IsSubmissionOpen = true
        });

        var typeId = Guid.NewGuid();

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = typeId,
            ConferenceId = RegisteredConferenceId,
            Name = "Akademisyen",
            Description = "Test",
            Price = 0,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Author"
        });

        // Yazar YALNIZCA erken tarihli kongreye kayıtlı
        db.Registrations.Add(new Registration
        {
            Id = RegistrationId,
            AppUserId = AuthorId,
            ConferenceId = RegisteredConferenceId,
            RegistrationTypeId = typeId,
            RegistrationDate = DateTime.UtcNow,
            IsPaid = false,
            Amount = 0
        });

        db.SaveChanges();
    }

    private async Task<(HttpStatusCode Code, string Path)> FollowAsync(string url, int max = 6)
    {
        for (var i = 0; i < max; i++)
        {
            var response = await _client.GetAsync(url);
            _output.WriteLine($"  {(int)response.StatusCode} {url}");

            if (response.Headers.Location is null)
            {
                return (response.StatusCode, url);
            }

            url = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location.PathAndQuery
                : response.Headers.Location.ToString();
        }

        return (HttpStatusCode.LoopDetected, url);
    }

    /// <summary>
    /// Kongrenin kendi slug'ıyla gidildiğinde bildiri gönderme ekranı açılmalı.
    /// </summary>
    [Fact]
    public async Task ConferenceSlug_ReachesSubmissionForm()
    {
        _output.WriteLine("kongre slug'ı ile:");
        var (code, final) = await FollowAsync($"/{RegisteredConfSlug}/submit-abstract");

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.DoesNotContain("/registration", final);
        Assert.DoesNotContain("MyConferences", final);
    }

    /// <summary>
    /// Gerçek kullanıcı akışı: kayıt tamamlanır, başarı sayfası açılır ve
    /// oradaki "Bildiri Gönderimine Geç" bağlantısına gidilir. Bağlantı kurum
    /// slug'ı taşıdığı için kurumda daha yeni tarihli başka bir kongre varsa
    /// kullanıcı yanlış kongreye düşüp kayıt sayfasına atılıyordu.
    /// </summary>
    [Fact]
    public async Task SuccessPage_ThenSubmitLink_ReachesSubmissionForm()
    {
        _output.WriteLine("başarı sayfası:");
        var success = await _client.GetAsync(
            $"/{TenantSlug}/registration/success?id={RegistrationId}");

        _output.WriteLine($"  {(int)success.StatusCode} başarı sayfası");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        _output.WriteLine("ardından bildiri gönderimine geç:");
        var (code, final) = await FollowAsync($"/{TenantSlug}/submit-abstract");

        Assert.Equal(HttpStatusCode.OK, code);
        Assert.DoesNotContain("/registration", final);
        Assert.DoesNotContain("MyConferences", final);
    }
}
