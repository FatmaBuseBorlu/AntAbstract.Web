using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CertificatesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICertificateService _certificateService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<CertificatesController> _localizer;

        public CertificatesController(
            AppDbContext context,
            ICertificateService certificateService,
            UserManager<AppUser> userManager,
            IStringLocalizer<CertificatesController> localizer)
        {
            _context = context;
            _certificateService = certificateService;
            _userManager = userManager;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            var user = await GetCurrentUserAsync();

            if (user == null || !user.TenantId.HasValue)
            {
                return null;
            }

            return user.TenantId.Value;
        }

        private async Task<bool> CanAccessCertificateAsync(Guid certificateId)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return false;
            }

            return await _context.Certificates
                .AsNoTracking()
                .Include(c => c.Conference)
                .AnyAsync(c =>
                    c.Id == certificateId &&
                    c.Conference != null &&
                    c.Conference.TenantId == tenantId.Value);
        }

        public async Task<IActionResult> Index(
            Guid? conferenceId = null,
            string? userEmail = null,
            CertificateType? type = null,
            bool onlyMissingFile = false,
            bool onlyEmailNotSent = false)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return View(Enumerable.Empty<Certificate>().ToList());
            }

            var query = _context.Certificates
                .AsNoTracking()
                .Include(x => x.Conference)
                .Include(x => x.User)
                .Where(x =>
                    x.Conference != null &&
                    x.Conference.TenantId == tenantId.Value)
                .AsQueryable();

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                var canAccessConference = await _context.Conferences
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == conferenceId.Value &&
                        x.TenantId == tenantId.Value);

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
    }
}