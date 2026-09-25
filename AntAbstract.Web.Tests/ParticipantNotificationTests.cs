using System.Collections.Concurrent;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>Kuyruğa giren e-postaları yakalar; SMTP'ye hiçbir şey gitmez.</summary>
public sealed class CapturingEmailQueue : IEmailQueue
{
    public ConcurrentBag<EmailQueueItem> Items { get; } = new();

    public void Enqueue(EmailQueueItem item) => Items.Add(item);

}

/// <summary>
/// Kayıt ve özet gönderiminde katılımcıya hiçbir onay gitmiyor, kongre
/// değişiklikleri kimseye ulaşmıyor, toplu duyuru yalnızca e-posta kalıyordu;
/// hatırlatma şablonları hiç eklenmediği için hatırlatmalar da gitmiyordu.
/// </summary>
public sealed class ParticipantNotificationTests : IClassFixture<AuthenticatedTestFactory>
{
    private static readonly Guid TenantId = new("f1f1f1f1-0000-0000-0000-0000000000f1");
    private static readonly Guid ConferenceId = new("f2f2f2f2-0000-0000-0000-0000000000f2");
    private static readonly Guid TypeId = new("f3f3f3f3-0000-0000-0000-0000000000f3");
    private static readonly Guid OtherTenantId = new("f1f1f1f1-0000-0000-0000-0000000000f9");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly CapturingEmailQueue _queue = new();

    public ParticipantNotificationTests(AuthenticatedTestFactory factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IEmailQueue>(_queue)));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        EmailTemplateDefaults.EnsureAsync(db).GetAwaiter().GetResult();

        if (db.Tenants.IgnoreQueryFilters().Any(t => t.Id == TenantId))
            return;

        db.Tenants.Add(new Tenant { Id = TenantId, Slug = "bildirim-kurum", Name = "Bildirim Kurumu" });
        db.Tenants.Add(new Tenant { Id = OtherTenantId, Slug = "bildirim-baska-kurum", Name = "Başka Kurum" });
        db.Conferences.Add(new Conference
        {
            Id = ConferenceId,
            TenantId = TenantId,
            Title = "Bildirim Kongresi",
            Slug = "bildirim-kongresi",
            StartDate = DateTime.Today.AddDays(30),
            EndDate = DateTime.Today.AddDays(31)
        });
        db.RegistrationTypes.Add(new RegistrationType
        {
            Id = TypeId,
            ConferenceId = ConferenceId,
            Name = "Yazar",
            Description = "Test",
            Price = 0,
            Currency = "TRY",
            IsActive = true,
            RoleName = "Author"
        });
        db.SaveChanges();
    }

    private async Task<AppUser> UserAsync(string id, bool confirmed = true, Guid? tenantId = null, string? role = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var user = await users.FindByIdAsync(id);
        if (user == null)
        {
            user = new AppUser
            {
                Id = id,
                UserName = $"{id}@antabstract.local",
                Email = $"{id}@antabstract.local",
                EmailConfirmed = confirmed,
                FirstName = "<b>Ayşe</b>",
                LastName = id,
                TenantId = tenantId
            };
            Assert.True((await users.CreateAsync(user)).Succeeded);
        }

        if (role != null)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
            if (!await users.IsInRoleAsync(user, role))
                await users.AddToRoleAsync(user, role);
        }

        return user;
    }

    private async Task<Guid> RegisterAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reg = new Registration { AppUserId = userId, ConferenceId = ConferenceId, RegistrationTypeId = TypeId };
        db.Registrations.Add(reg);
        await db.SaveChangesAsync();
        return reg.Id;
    }

    private List<Notification> NotificationsOf(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .IgnoreQueryFilters().AsNoTracking().Where(n => n.UserId == userId).ToList();
    }

    private List<EmailQueueItem> MailsTo(string userId) =>
        _queue.Items.Where(i => i.To == $"{userId}@antabstract.local").ToList();

    [Fact]
    public void AllTemplatesTheSystemSends_ExistAfterStartupSeeding()
    {
        using var scope = _factory.Services.CreateScope();
        var keys = scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailTemplates.Select(t => t.Key).ToList();

        foreach (var key in new[]
                 {
                     "decision.accept", "decision.reject", "decision.revision",
                     "certificate.author", "certificate.reviewer", "certificate.attendee",
                     EmailTemplateDefaults.DeadlineReminder, EmailTemplateDefaults.PaymentPendingReminder,
                     EmailTemplateDefaults.RegistrationReceived, EmailTemplateDefaults.SubmissionReceived,
                     EmailTemplateDefaults.ConferenceUpdated
                 })
        {
            Assert.Contains(key, keys);
        }
    }

    [Fact]
    public async Task EnsureDefaults_DoesNotOverwriteAdminEdits()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var template = db.EmailTemplates.First(t => t.Key == "decision.accept");
        template.Subject = "Yöneticinin konusu";
        await db.SaveChangesAsync();

        Assert.Equal(0, await EmailTemplateDefaults.EnsureAsync(db));
        Assert.Equal("Yöneticinin konusu", db.EmailTemplates.AsNoTracking().First(t => t.Key == "decision.accept").Subject);
    }

    [Fact]
    public async Task Registration_SendsEmailAndNotification_WithEncodedName()
    {
        var user = await UserAsync("bildirim-kayit");
        var regId = await RegisterAsync(user.Id);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ParticipantNotifier>().RegistrationReceivedAsync(regId);

        var mail = Assert.Single(MailsTo(user.Id));
        Assert.Contains("Bildirim Kongresi", mail.Subject);
        Assert.Contains("&lt;b&gt;Ayşe&lt;/b&gt;", mail.HtmlBody);
        Assert.Contains("/bildirim-kongresi/submit-abstract", mail.HtmlBody);
        Assert.Contains(NotificationsOf(user.Id), n => n.Title is "Ön kaydınız alındı" or "Pre-registration received");
    }

    [Fact]
    public async Task Registration_ForUnconfirmedEmail_OnlyInApp()
    {
        var user = await UserAsync("bildirim-dogrulanmamis", confirmed: false);
        var regId = await RegisterAsync(user.Id);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ParticipantNotifier>().RegistrationReceivedAsync(regId, sendEmail: false);

        Assert.Empty(MailsTo(user.Id));
        Assert.NotEmpty(NotificationsOf(user.Id));
    }

    [Fact]
    public async Task Submission_NotifiesAuthor_AndOnlyAdminsOfThatTenant()
    {
        var author = await UserAsync("bildirim-yazar");
        var admin = await UserAsync("bildirim-admin", tenantId: TenantId, role: "Admin");
        var referee = await UserAsync("bildirim-hakem", tenantId: TenantId, role: "Referee");
        var otherAdmin = await UserAsync("bildirim-baska-admin", tenantId: OtherTenantId, role: "Admin");

        var submission = new Submission
        {
            ConferenceId = ConferenceId,
            TenantId = TenantId,
            AuthorId = author.Id,
            Title = "Test özeti",
            Abstract = "Özet",
            Keywords = "test",
            PresentationType = "Oral",
            Status = SubmissionStatus.New,
            CreatedDate = DateTime.UtcNow
        };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Submissions.Add(submission);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ParticipantNotifier>().SubmissionReceivedAsync(submission.Id);
        }

        var mail = Assert.Single(MailsTo(author.Id));
        Assert.Contains(submission.SubmissionIdCode, mail.Subject);
        Assert.NotEmpty(NotificationsOf(author.Id));
        Assert.Contains(NotificationsOf(admin.Id), n => n.Title == "Yeni bildiri özeti");
        Assert.Empty(NotificationsOf(referee.Id));
        Assert.Empty(NotificationsOf(otherAdmin.Id));
    }

    [Fact]
    public async Task ConferenceUpdate_ReachesEachRegistrantOnce_WithOldAndNewValues()
    {
        var a = await UserAsync("bildirim-katilimci-a");
        var b = await UserAsync("bildirim-katilimci-b");
        await RegisterAsync(a.Id);
        await RegisterAsync(b.Id);

        int sent;
        using (var scope = _factory.Services.CreateScope())
        {
            sent = await scope.ServiceProvider.GetRequiredService<ParticipantNotifier>().ConferenceUpdatedAsync(
                ConferenceId,
                new[] { new ParticipantNotifier.FieldChange("Özet son tarihi", "Abstract deadline", "01.11.2026 23:59", "15.11.2026 23:59") });
        }

        Assert.True(sent >= 2);
        var mail = Assert.Single(MailsTo(a.Id), m => m.Subject.Contains("güncellendi"));
        Assert.Contains("01.11.2026 23:59", mail.HtmlBody);
        Assert.Contains("15.11.2026 23:59", mail.HtmlBody);
        Assert.Single(NotificationsOf(b.Id), n => n.Title == "Kongre bilgileri güncellendi");
    }

    [Fact]
    public async Task Broadcast_AlsoCreatesInAppNotifications()
    {
        var user = await UserAsync("bildirim-duyuru");

        using var scope = _factory.Services.CreateScope();
        var count = await BroadcastInAppNotifier.NotifyAsync(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<INotificationService>(),
            ConferenceId,
            new[] { "BILDIRIM-DUYURU@antabstract.local", "yok@x.local" },
            "Program yayınlandı",
            "<p>Kongre <strong>programı</strong> yayınlandı.</p>");

        Assert.Equal(1, count);
        var n = Assert.Single(NotificationsOf(user.Id));
        Assert.Equal("Program yayınlandı", n.Title);
        Assert.DoesNotContain("<", n.Message);
    }
}
