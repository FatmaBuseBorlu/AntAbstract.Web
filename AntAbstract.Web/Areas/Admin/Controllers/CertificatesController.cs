using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class CertificatesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICertificateService _certificateService;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IStringLocalizer<CertificatesController> _localizer;
        private readonly ILogger<CertificatesController> _logger;
        private readonly INotificationService _notificationService;

        public CertificatesController(
            AppDbContext context,
            ICertificateService certificateService,
            IAdminTenantAccessService tenantAccess,
            IStringLocalizer<CertificatesController> localizer,
            ILogger<CertificatesController> logger,
            INotificationService notificationService)
        {
            _context = context;
            _certificateService = certificateService;
            _tenantAccess = tenantAccess;
            _localizer = localizer;
            _logger = logger;
            _notificationService = notificationService;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private bool IsSuperAdmin()
        {
            return User.IsInRole("SuperAdmin");
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var query = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            return query.AsNoTracking();
        }

        /// <summary>
        /// SuperAdmin hiçbir kuruma bağlı değil (TenantId = null), bu yüzden
        /// kurum kimliğine göre filtre onu tümüyle dışarıda bırakıyordu.
        /// Kapsam artık erişilebilir kongre sorgusundan geliyor: kurum admini
        /// yalnızca kendi kongrelerini, SuperAdmin hepsini görüyor.
        /// </summary>
        private async Task<bool> CanAccessCertificateAsync(Guid certificateId)
        {
            var accessible = await GetAccessibleConferenceQueryAsync();

            return await _context.Certificates
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Id == certificateId &&
                    accessible.Any(conference => conference.Id == c.ConferenceId));
        }

        public async Task<IActionResult> Index(
            Guid? conferenceId = null,
            string? userEmail = null,
            CertificateType? type = null,
            bool onlyMissingFile = false,
            bool onlyEmailNotSent = false)
        {
            // Kurum zorunluluğu yalnızca kurum adminleri için geçerli;
            // SuperAdmin'in kurumu yok ama tüm kongrelere erişiyor.
            if (!IsSuperAdmin())
            {
                var tenantId = await GetCurrentAdminTenantIdAsync();

                if (!tenantId.HasValue)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_AdminTenantNotFound",
                        "Admin hesabınıza bağlı kurum bulunamadı.");

                    return View(Enumerable.Empty<Certificate>().ToList());
                }
            }

            var accessible = await GetAccessibleConferenceQueryAsync();

            var query = _context.Certificates
                .AsNoTracking()
                .Include(x => x.Conference)
                .Include(x => x.User)
                .Where(x => accessible.Any(conference => conference.Id == x.ConferenceId))
                .AsQueryable();

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                var canAccessConference = await accessible
                    .AnyAsync(x => x.Id == conferenceId.Value);

                if (!canAccessConference)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_ConferenceUnauthorized",
                        "Bu kongreye ait sertifikaları görüntüleme yetkiniz yok.");

                    return View(Enumerable.Empty<Certificate>().ToList());
                }

                query = query.Where(x => x.ConferenceId == conferenceId.Value);
            }

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var email = userEmail.Trim();

                query = query.Where(x =>
                    x.User != null &&
                    x.User.Email != null &&
                    x.User.Email.Contains(email));
            }

            if (type.HasValue)
            {
                query = query.Where(x => x.Type == type.Value);
            }

            if (onlyMissingFile)
            {
                query = query.Where(x =>
                    x.GeneratedAt == null ||
                    x.FilePath == null ||
                    x.FilePath == "");
            }

            if (onlyEmailNotSent)
            {
                query = query.Where(x => x.EmailSentAt == null);
            }

            var list = await query
                .OrderByDescending(x => x.EligibleAt)
                .Take(300)
                .ToListAsync();

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Regenerate(Guid id)
        {
            if (!await CanAccessCertificateAsync(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedRegenerate",
                    "Bu sertifikayı yeniden oluşturma yetkiniz yok.");

                return RedirectToAction(nameof(Index));
            }

            await _certificateService.RegenerateCertificateFileAsync(id, resendEmail: false);

            TempData["SuccessMessage"] = T(
                "Success_CertificateRegenerated",
                "Sertifika dosyası başarıyla yeniden oluşturuldu.");

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmail(Guid id)
        {
            if (!await CanAccessCertificateAsync(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedResendEmail",
                    "Bu sertifika e-postasını yeniden gönderme yetkiniz yok.");

                return RedirectToAction(nameof(Index));
            }

            await _certificateService.ResendCertificateEmailAsync(id);

            TempData["SuccessMessage"] = T(
                "Success_CertificateEmailResent",
                "Sertifika e-postası başarıyla yeniden gönderildi.");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            if (!await CanAccessCertificateAsync(id))
            {
                return Unauthorized(T(
                    "Error_UnauthorizedDownload",
                    "Bu sertifikayı indirme yetkiniz yok."));
            }

            var bytes = await _certificateService.GetCertificateFileAdminAsync(id);

            if (bytes == null)
            {
                return NotFound(T(
                    "Error_CertificateFileNotFound",
                    "Sertifika dosyası bulunamadı."));
            }

            return File(
                bytes,
                "application/pdf",
                $"certificate_{id}.pdf");
        }

        // ── Bulk Trigger ─────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TriggerBulk(Guid conferenceId)
        {
            if (!IsSuperAdmin())
            {
                var tenantId = await GetCurrentAdminTenantIdAsync();

                if (!tenantId.HasValue)
                {
                    TempData["ErrorMessage"] = T("Error_AdminTenantNotFound", "Admin hesabınıza bağlı kurum bulunamadı.");
                    return RedirectToAction("Index", new { conferenceId });
                }
            }

            // Erişim kısıtı erişilebilir kongre sorgusunda: kurum admini yalnızca
            // kendi kongresini, SuperAdmin hepsini bulabilir.
            var accessible = await GetAccessibleConferenceQueryAsync();

            var conference = await accessible
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T("Msg_KongreBulunamadi", "Kongre bulunamadı.");
                return RedirectToAction("Index");
            }

            int generated = 0;
            int errors = 0;

            // 1. Author certificates — all accepted/presented submissions
            // User/UserId, Author/AuthorId'nin [NotMapped] kısayolları; sorguda
            // kullanılamaz çünkü veritabanına çevrilemiyor.
            var authorSubmissions = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == conferenceId &&
                    (s.Status == AntAbstract.Domain.Entities.SubmissionStatus.Accepted ||
                     s.Status == AntAbstract.Domain.Entities.SubmissionStatus.Presented) &&
                    s.AuthorId != "")
                .ToListAsync();

            foreach (var sub in authorSubmissions)
            {
                try
                {
                    await _certificateService.EnsureAuthorCertificateAsync(conferenceId, sub.UserId!);
                    generated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Yazar sertifikası oluşturulamadı. UserId={UserId}", sub.UserId);
                    errors++;
                }
            }

            // 2. Reviewer certificates — all who completed at least one review
            var reviewerIds = await _context.ReviewAssignments
                .AsNoTracking()
                .Include(ra => ra.Submission)
                .Include(ra => ra.Review)
                .Where(ra => ra.Submission != null &&
                    ra.Submission.ConferenceId == conferenceId &&
                    ra.Review != null && ra.Review.Score > 0)
                .Select(ra => ra.ReviewerId)
                .Distinct()
                .ToListAsync();

            foreach (var reviewerId in reviewerIds)
            {
                try
                {
                    await _certificateService.EnsureReviewerCertificateAsync(conferenceId, reviewerId);
                    generated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Hakem sertifikası oluşturulamadı. ReviewerId={ReviewerId}", reviewerId);
                    errors++;
                }
            }

            // 3. Attendee certificates — all with completed attendance
            var attendeeIds = await _context.ConferenceAttendances
                .AsNoTracking()
                .Where(a => a.ConferenceId == conferenceId &&
                    (a.CompletedAt.HasValue || a.TotalSeconds >= a.RequiredSeconds) &&
                    a.UserId != null)
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var attendeeId in attendeeIds)
            {
                try
                {
                    await _certificateService.EnsureAttendeeCertificateAsync(conferenceId, attendeeId!);
                    generated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Katılımcı sertifikası oluşturulamadı. UserId={UserId}", attendeeId);
                    errors++;
                }
            }

            var allUserIds = authorSubmissions.Select(s => s.UserId!)
                .Union(reviewerIds)
                .Union(attendeeIds.Where(id => id != null).Select(id => id!))
                .Distinct()
                .ToList();

            foreach (var uid in allUserIds)
            {
                try
                {
                    await _notificationService.CreateAsync(
                        userId: uid,
                        title: "Sertifikanız Hazır",
                        message: $"{conference.Title} kongresine ait sertifikanız oluşturuldu.",
                        icon: "fas fa-certificate",
                        color: "success",
                        link: "/Certificates/Index");
                }
                catch { }
            }

            if (errors == 0)
                TempData["SuccessMessage"] = $"Toplu sertifika oluşturma tamamlandı. {generated} sertifika işlendi, {allUserIds.Count} kullanıcıya bildirim gönderildi.";
            else
                TempData["SuccessMessage"] = $"{generated} sertifika işlendi, {errors} hata oluştu.";

            return RedirectToAction("Index", new { conferenceId });
        }
    }
}
