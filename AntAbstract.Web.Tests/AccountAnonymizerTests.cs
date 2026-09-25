using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>
/// "Kişisel verilerimi sil" eskiden kullanıcı satırını siliyordu: hakemde
/// yabancı anahtar yüzünden 500 veriyor, yazarda bildirileri de götürüyordu.
/// Artık hesap anonimleştiriliyor; bildiriler ve değerlendirmeler kalıyor.
/// </summary>
public sealed class AccountAnonymizerTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;

    private static readonly Guid TenantId = new("a1a1a1a1-0000-0000-0000-00000000a1a1");
    private static readonly Guid ConferenceId = new("a2a2a2a2-0000-0000-0000-00000000a2a2");
    private static readonly Guid SubmissionId = new("a3a3a3a3-0000-0000-0000-00000000a3a3");
    private const string Email = "silinecek-yazar@antabstract.local";

    public AccountAnonymizerTests(AuthenticatedTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Author_IsAnonymized_AndSubmissionStays()
    {
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            db.Tenants.Add(new Tenant { Id = TenantId, Slug = "anonim-kurum", Name = "Anonim Kurum" });
            db.Conferences.Add(new Conference
            {
                Id = ConferenceId,
                TenantId = TenantId,
                Title = "Anonimleştirme Kongresi",
                Slug = "anonim-kongre",
                StartDate = DateTime.Today.AddDays(30),
                EndDate = DateTime.Today.AddDays(31)
            });
            await db.SaveChangesAsync();

            var user = new AppUser
            {
                UserName = Email,
                Email = Email,
                FirstName = "Ayşe",
                LastName = "Yılmaz",
                PhoneNumber = "05550000000",
                Institution = "Örnek Üniversitesi",
                IdentityNumber = "11111111111"
            };
            Assert.True((await users.CreateAsync(user, "Sifre1234")).Succeeded);
            userId = user.Id;

            db.Submissions.Add(new Submission
            {
                Id = SubmissionId,
                ConferenceId = ConferenceId,
                TenantId = TenantId,
                AuthorId = userId,
                Title = "Kalması gereken bildiri",
                Abstract = "Özet",
                Keywords = "test",
                PresentationType = "Oral",
                Status = SubmissionStatus.Accepted,
                CreatedDate = DateTime.UtcNow
            });
            db.Notifications.Add(new Notification { UserId = userId, Title = "Bildirim" });
            await db.SaveChangesAsync();

            var anonymizer = scope.ServiceProvider.GetRequiredService<AccountAnonymizer>();
            Assert.True((await anonymizer.AnonymizeAsync(user)).Succeeded);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var user = await users.FindByIdAsync(userId);
            Assert.NotNull(user);
            Assert.True(AccountAnonymizer.IsAnonymized(user!));
            Assert.Null(await users.FindByEmailAsync(Email));
            Assert.False(await users.HasPasswordAsync(user!));
            Assert.True(await users.IsLockedOutAsync(user!));
            Assert.Null(user!.PhoneNumber);
            Assert.Null(user.Institution);
            Assert.Null(user.IdentityNumber);
            Assert.NotEqual("Ayşe", user.FirstName);

            Assert.True(await db.Submissions.IgnoreQueryFilters().AnyAsync(s => s.Id == SubmissionId));
            Assert.False(await db.Notifications.IgnoreQueryFilters().AnyAsync(n => n.UserId == userId));
        }
    }
}
