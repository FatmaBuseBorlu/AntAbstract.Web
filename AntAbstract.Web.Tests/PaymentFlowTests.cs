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
/// Ödeme zinciri: kabul edilmiş bildirisi olan katılımcı ödeme adımına
/// geçer, havale seçer, kayıt oluşur; yönetici onaylayınca kayıt ödenmiş
/// duruma geçer. Ödeme gönderimi bir rota çakışması yüzünden 500 veriyordu
/// ve üretimde hiç ödeme kaydı oluşmamıştı.
/// </summary>
public sealed class PaymentFlowTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _author;
    private readonly ITestOutputHelper _output;

    private const string TenantSlug = "odeme-akis-kurum";
    private const string ConferenceSlug = "odeme-akis-kongre";
    private const string UserId = "odeme-akis-yazar";

    private static readonly Guid TenantId = new("77771111-2222-3333-4444-555566667777");
    private static readonly Guid ConferenceId = new("88881111-2222-3333-4444-555566667777");
    private static readonly Guid RegistrationTypeId = new("99991111-2222-3333-4444-555566667777");
    private static readonly Guid SubmissionId = new("aaaa1111-2222-3333-4444-555566667777");
    private static readonly Guid RegistrationId = new("bbbb1111-2222-3333-4444-555566667777");

    public PaymentFlowTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        _admin = factory.CreateClient(new() { AllowAutoRedirect = false });

        _author = factory.CreateClient(new() { AllowAutoRedirect = false });
        _author.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        _author.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, UserId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "odeme.akis@test.local",
            NormalizedUserName = "ODEME.AKIS@TEST.LOCAL",
            Email = "odeme.akis@test.local",
            NormalizedEmail = "ODEME.AKIS@TEST.LOCAL",
            FirstName = "Ödeme",
            LastName = "Yazar",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = TenantSlug, Name = "Ödeme Akış Üniversitesi" });

        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Ödeme Akış Kongresi",
            Slug = ConferenceSlug,
            StartDate = DateTime.Today.AddDays(40),
            EndDate = DateTime.Today.AddDays(42),
            IsRegistrationOpen = true,
            IsSubmissionOpen = true
        });

        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = RegistrationTypeId,
            ConferenceId = ConferenceId,
            Name = "Bildirili Katılım",
            Description = "Bildiri gönderenler için",
            Price = 1000m,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Author"
        });

        // Ödeme adımı yalnızca kabul edilmiş bildirisi olana açılıyor.
        db.Submissions.Add(new Submission
        {
            Id = SubmissionId,
            ConferenceId = ConferenceId,
            TenantId = TenantId,
            AuthorId = UserId,
            Title = "Kabul Edilmiş Bildiri",
            Abstract = "Özet",
            Keywords = "test",
            PresentationType = "Oral",
            Status = SubmissionStatus.Accepted,
            CreatedDate = DateTime.UtcNow
        });

        db.Registrations.Add(new Registration
        {
            Id = RegistrationId,
            AppUserId = UserId,
            ConferenceId = ConferenceId,
            RegistrationTypeId = RegistrationTypeId,
            RegistrationDate = DateTime.UtcNow,
            IsPaid = false,
            Amount = 1000m
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
    public async Task HavaleOdemesi_OlusurVeYoneticiOnayiylaOdenmisOlur()
    {
        // 1. Ödeme adımı açılmalı ve yöntemleri listelemeli.
        var checkout = await FollowAsync(
            _author, $"/{TenantSlug}/payment/checkout/{RegistrationId}");

        Assert.Equal(HttpStatusCode.OK, checkout.Response.StatusCode);
        Assert.Contains("BankTransfer", checkout.Html, StringComparison.Ordinal);

        // 2. Havale seçimi ödeme kaydı oluşturmalı. Bu adım rota çakışması
        //    yüzünden 500 veriyordu.
        var processed = await _author.PostAsync(
            $"/{TenantSlug}/payment/process",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(checkout.Html),
                ["RelatedSubmissionId"] = RegistrationId.ToString(),
                ["PaymentMethod"] = "BankTransfer",
                ["Amount"] = "1000"
            }));

        Assert.True(
            (int)processed.StatusCode < 500,
            $"Ödeme gönderimi {(int)processed.StatusCode} döndü.");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var payment = await db.Payments.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.RelatedSubmissionId == RegistrationId);

            Assert.True(payment != null, "Ödeme kaydı oluşmadı.");
            Assert.Equal("BankTransfer", payment!.PaymentMethod);
            Assert.Equal(PaymentStatus.Pending, payment.Status);

            _output.WriteLine($"{payment.PaymentMethod} {payment.Amount} [{payment.Status}]");
        }

        // 3. Dekont yükleme ekranı açılmalı.
        var receipt = await FollowAsync(
            _author, $"/{TenantSlug}/payment/upload-receipt/{RegistrationId}");

        Assert.Equal(HttpStatusCode.OK, receipt.Response.StatusCode);

        // 4. Yönetici onayı kaydı ödenmiş yapmalı.
        var page = await FollowAsync(
            _admin,
            $"/{TenantSlug}/Admin/ConferenceFlow/RegistrationsAndPayments?conferenceId={ConferenceId}");

        Assert.Equal(HttpStatusCode.OK, page.Response.StatusCode);

        var approved = await _admin.PostAsync(
            $"/{TenantSlug}/Admin/ConferenceFlow/RegistrationsAndPayments/ApprovePayment",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(page.Html),
                ["registrationId"] = RegistrationId.ToString(),
                ["conferenceId"] = ConferenceId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var registration = await db.Registrations.IgnoreQueryFilters()
                .FirstAsync(r => r.Id == RegistrationId);

            Assert.True(registration.IsPaid, "Onaya rağmen kayıt ödenmiş olmadı.");
        }
    }

    /// <summary>Ödeme ekranları açılmalı.</summary>
    [Theory]
    [InlineData("Ödemelerim", "/payments")]
    [InlineData("Ödeme başarılı", "/payment/success")]
    [InlineData("Ödeme iptal", "/payment/cancel")]
    public async Task OdemeEkranlari_500Vermiyor(string ad, string path)
    {
        var response = await _author.GetAsync($"/{TenantSlug}{path}");

        _output.WriteLine($"{(int)response.StatusCode} {ad}");

        Assert.True((int)response.StatusCode < 500, $"{ad} {(int)response.StatusCode} döndü.");
    }
}
