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
    [Authorize(Roles = "Admin,Organizator")]
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

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        private async Task<bool> CanAccessCertificateAsync(Guid certificateId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return false;
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                return true;
            }

            if (!user.TenantId.HasValue)
            {
                return false;
            }

            var certificate = await _context.Certificates
                .AsNoTracking()
                .Include(c => c.Conference)
                .FirstOrDefaultAsync(c => c.Id == certificateId);

            return certificate != null &&
                   certificate.Conference != null &&
                   certificate.Conference.TenantId == user.TenantId.Value;
        }

        public async Task<IActionResult> Index(
            Guid? conferenceId = null,
            string? userEmail = null,
            CertificateType? type = null,
            bool onlyMissingFile = false,
            bool onlyEmailNotSent = false)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            var query = _context.Certificates
                .AsNoTracking()
                .Include(x => x.Conference)
                .Include(x => x.User)
                .AsQueryable();

            if (!isAdmin)
            {
                if (currentUser.TenantId.HasValue)
                {
                    query = query.Where(x =>
                        x.Conference != null &&
                        x.Conference.TenantId == currentUser.TenantId.Value);
                }
                else
                {
                    query = query.Where(x => false);
                }
            }

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
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