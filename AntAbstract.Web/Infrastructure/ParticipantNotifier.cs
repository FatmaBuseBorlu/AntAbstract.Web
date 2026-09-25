using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;

namespace AntAbstract.Web.Infrastructure
{
    /// <summary>
    /// Katılımcıya ve kongre yöneticisine giden onay/duyuru bildirimleri
    /// (e-posta + sistem içi). Eskiden kayıt ve özet gönderiminde hiçbir şey
    /// gitmiyor, kongre değişiklikleri kimseye ulaşmıyordu.
    ///
    /// Hiçbir yöntem hata fırlatmaz: bildirim gidemezse asıl işlem (kayıt,
    /// özet, kaydetme) yine tamamlanmalı. E-postalar arka plan kuyruğundan
    /// gider; istek SMTP'yi beklemez.
    /// </summary>
    public class ParticipantNotifier
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _email;
        private readonly INotificationService _notifications;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<ParticipantNotifier> _logger;

        public ParticipantNotifier(
            AppDbContext context,
            IEmailService email,
            INotificationService notifications,
            UserManager<AppUser> userManager,
            IConfiguration configuration,
            IHttpContextAccessor http,
            ILogger<ParticipantNotifier> logger)
        {
            _context = context;
            _email = email;
            _notifications = notifications;
            _userManager = userManager;
            _configuration = configuration;
            _http = http;
            _logger = logger;
        }

        private static bool En => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

        private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? "");

        private static string FullName(AppUser user)
        {
            var name = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? user.Email ?? "" : name;
        }

        private static string SlugOf(Conference conference) =>
            string.IsNullOrWhiteSpace(conference.Slug) ? conference.Tenant?.Slug ?? "" : conference.Slug;

        private string AbsoluteUrl(string path)
        {
            var configured = _configuration["Email:BaseUrl"];
            string baseUrl;

            if (!string.IsNullOrWhiteSpace(configured) && !configured.Contains("#{") &&
                Uri.TryCreate(configured, UriKind.Absolute, out var uri))
            {
                baseUrl = uri.GetLeftPart(UriPartial.Authority);
            }
            else
            {
                var request = _http.HttpContext?.Request;
                baseUrl = request == null ? "" : $"{request.Scheme}://{request.Host}{request.PathBase}";
            }

            return baseUrl.TrimEnd('/') + path;
        }

        // ── 1. Ön kayıt ────────────────────────────────────────────────────

        /// <param name="sendEmail">
        /// Hesap açılışıyla birlikte yapılan ön kayıtta false: adres henüz
        /// doğrulanmadı, o anda zaten doğrulama e-postası gidiyor.
        /// </param>
        public async Task RegistrationReceivedAsync(Guid registrationId, bool sendEmail = true)
        {
            try
            {
                var registration = await _context.Registrations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(r => r.AppUser)
                    .Include(r => r.RegistrationType)
                    .Include(r => r.Conference).ThenInclude(c => c.Tenant)
                    .FirstOrDefaultAsync(r => r.Id == registrationId);

                if (registration?.AppUser == null || registration.Conference == null)
                    return;

                var user = registration.AppUser;
                var conference = registration.Conference;
                var nextStep = $"/{SlugOf(conference)}/submit-abstract";

                await _notifications.CreateAsync(
                    userId: user.Id,
                    title: En ? "Pre-registration received" : "Ön kaydınız alındı",
                    message: En
                        ? $"Your pre-registration for {conference.Title} is received. Next step: submit your abstract."
                        : $"{conference.Title} kongresine ön kaydınız alındı. Sıradaki adım: bildiri özetinizi göndermek.",
                    icon: "✅",
                    color: "success",
                    link: nextStep);

                if (sendEmail && !string.IsNullOrWhiteSpace(user.Email))
                {
                    await _email.EnqueueTemplatedAsync(user.Email, EmailTemplateDefaults.RegistrationReceived,
                        new Dictionary<string, string>
                        {
                            ["{FullName}"] = Enc(FullName(user)),
                            ["{ConferenceTitle}"] = Enc(conference.Title),
                            ["{RegistrationType}"] = Enc(registration.RegistrationType?.Name),
                            ["{NextStepLink}"] = AbsoluteUrl(nextStep),
                            ["{ConferenceLink}"] = AbsoluteUrl($"/{SlugOf(conference)}")
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ön kayıt bildirimi gönderilemedi. RegistrationId={Id}", registrationId);
            }
        }

        // ── 2. Özet gönderimi ──────────────────────────────────────────────

        public async Task SubmissionReceivedAsync(Guid submissionId)
        {
            try
            {
                var submission = await _context.Submissions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(s => s.Author)
                    .Include(s => s.Conference).ThenInclude(c => c.Tenant)
                    .FirstOrDefaultAsync(s => s.Id == submissionId);

                if (submission?.Author == null || submission.Conference == null)
                    return;

                var author = submission.Author;
                var conference = submission.Conference;
                var submissionNo = string.IsNullOrWhiteSpace(submission.SubmissionIdCode)
                    ? submissionId.ToString("N")[..8].ToUpperInvariant()
                    : submission.SubmissionIdCode;
                var link = $"/{SlugOf(conference)}/my-submissions";

                await _notifications.CreateAsync(
                    userId: author.Id,
                    title: En ? "Abstract received" : "Bildiri özetiniz alındı",
                    message: En
                        ? $"\"{submission.Title}\" ({submissionNo}) has been received for {conference.Title}."
                        : $"\"{submission.Title}\" ({submissionNo}) başlıklı özetiniz {conference.Title} için alındı.",
                    icon: "📄",
                    color: "success",
                    link: link);

                if (!string.IsNullOrWhiteSpace(author.Email))
                {
                    await _email.EnqueueTemplatedAsync(author.Email, EmailTemplateDefaults.SubmissionReceived,
                        new Dictionary<string, string>
                        {
                            ["{FullName}"] = Enc(FullName(author)),
                            ["{ConferenceTitle}"] = Enc(conference.Title),
                            ["{SubmissionTitle}"] = Enc(submission.Title),
                            ["{SubmissionNo}"] = submissionNo,
                            ["{SubmissionLink}"] = AbsoluteUrl(link)
                        });
                }

                // Yöneticiye: yalnızca bu kongrenin kurumundaki Admin'ler (hakemler
                // de kuruma bağlı; kör değerlendirmede onlara yazar bildirimi gitmez).
                var admins = (await _userManager.GetUsersInRoleAsync("Admin"))
                    .Where(u => u.TenantId == conference.TenantId)
                    .ToList();

                foreach (var admin in admins)
                {
                    await _notifications.CreateAsync(
                        userId: admin.Id,
                        title: "Yeni bildiri özeti",
                        message: $"{conference.Title}: \"{submission.Title}\" ({submissionNo})",
                        icon: "📥",
                        color: "info",
                        link: $"/{conference.Tenant?.Slug ?? SlugOf(conference)}/Admin/Submissions/Details/{submission.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Özet alındı bildirimi gönderilemedi. SubmissionId={Id}", submissionId);
            }
        }

        // ── 3. Kongre bilgisi değişikliği ──────────────────────────────────

        public record FieldChange(string LabelTr, string LabelEn, string Before, string After);

        /// <summary>Kayıtlı herkese (tekil kullanıcı) e-posta + bildirim. Gönderilen kişi sayısını döner.</summary>
        public async Task<int> ConferenceUpdatedAsync(Guid conferenceId, IReadOnlyList<FieldChange> changes)
        {
            if (changes.Count == 0)
                return 0;

            try
            {
                var conference = await _context.Conferences
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Id == conferenceId);

                if (conference == null)
                    return 0;

                var recipientIds = await _context.Registrations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.ConferenceId == conferenceId)
                    .Select(r => r.AppUserId)
                    .Distinct()
                    .ToListAsync();

                var recipients = await _context.Users
                    .AsNoTracking()
                    .Where(u => recipientIds.Contains(u.Id))
                    .ToListAsync();

                var rows = string.Join("", changes.Select(c =>
                    $"<li><strong>{Enc(c.LabelTr)} / {Enc(c.LabelEn)}:</strong> " +
                    $"<span style='color:#9ca3af;text-decoration:line-through'>{Enc(c.Before)}</span> → " +
                    $"<strong>{Enc(c.After)}</strong></li>"));
                var changesHtml = $"<ul style='padding-left:18px'>{rows}</ul>";
                var summaryTr = string.Join(", ", changes.Select(c => $"{c.LabelTr}: {c.After}"));
                var link = $"/{SlugOf(conference)}";

                var sent = 0;
                foreach (var user in recipients)
                {
                    await _notifications.CreateAsync(
                        userId: user.Id,
                        title: "Kongre bilgileri güncellendi",
                        message: $"{conference.Title} — {summaryTr}",
                        icon: "📢",
                        color: "warning",
                        link: link);

                    if (!string.IsNullOrWhiteSpace(user.Email) && !AccountAnonymizer.IsAnonymized(user))
                    {
                        await _email.EnqueueTemplatedAsync(user.Email, EmailTemplateDefaults.ConferenceUpdated,
                            new Dictionary<string, string>
                            {
                                ["{FullName}"] = Enc(FullName(user)),
                                ["{ConferenceTitle}"] = Enc(conference.Title),
                                ["{Changes}"] = changesHtml,
                                ["{ConferenceLink}"] = AbsoluteUrl(link)
                            });
                    }

                    sent++;
                }

                return sent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kongre güncelleme bildirimi gönderilemedi. ConferenceId={Id}", conferenceId);
                return 0;
            }
        }
    }
}
