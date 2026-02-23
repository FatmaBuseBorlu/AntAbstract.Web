using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class CertificatesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICertificateService _certificateService;
        private readonly UserManager<AppUser> _userManager;

        public CertificatesController(
            AppDbContext context,
            ICertificateService certificateService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _certificateService = certificateService;
            _userManager = userManager;
        }

        private async Task<bool> IsAuthorizedForCertificate(Guid certId)
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin) return true; 
            if (user?.TenantId == null) return false; 

            var cert = await _context.Certificates
                .AsNoTracking()
                .Include(c => c.Conference)
                .FirstOrDefaultAsync(c => c.Id == certId);

            return cert != null && cert.Conference != null && cert.Conference.TenantId == user.TenantId.Value;
        }

        public async Task<IActionResult> Index(
            Guid? conferenceId = null,
            string? userEmail = null,
            CertificateType? type = null,
            bool onlyMissingFile = false,
            bool onlyEmailNotSent = false)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "Admin");

            var q = _context.Certificates
                .AsNoTracking()
                .Include(x => x.Conference)
                .Include(x => x.User)
                .AsQueryable();

            if (!isAdmin && currentUser?.TenantId != null)
            {
                q = q.Where(x => x.Conference.TenantId == currentUser.TenantId.Value);
            }
            else if (!isAdmin && currentUser?.TenantId == null)
            {
                q = q.Where(x => false); 
            }

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                q = q.Where(x => x.ConferenceId == conferenceId.Value);

            if (!string.IsNullOrWhiteSpace(userEmail))
                q = q.Where(x => x.User.Email != null && x.User.Email.Contains(userEmail));

            if (type.HasValue)
                q = q.Where(x => x.Type == type.Value);

            if (onlyMissingFile)
                q = q.Where(x => x.GeneratedAt == null || x.FilePath == null || x.FilePath == "");

            if (onlyEmailNotSent)
                q = q.Where(x => x.EmailSentAt == null);

            var list = await q
                .OrderByDescending(x => x.EligibleAt)
                .Take(300)
                .ToListAsync();

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Regenerate(Guid id)
        {
            if (!await IsAuthorizedForCertificate(id))
            {
                TempData["ErrorMessage"] = "Yetkisiz işlem! Başka bir kuruma ait sertifikayı üretemezsiniz.";
                return RedirectToAction(nameof(Index));
            }

            await _certificateService.RegenerateCertificateFileAsync(id, resendEmail: false);
            TempData["SuccessMessage"] = "Sertifika dosyası yeniden üretildi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmail(Guid id)
        {
            if (!await IsAuthorizedForCertificate(id))
            {
                TempData["ErrorMessage"] = "Yetkisiz işlem! Başka bir kuruma ait sertifikayı gönderemezsiniz.";
                return RedirectToAction(nameof(Index));
            }

            await _certificateService.ResendCertificateEmailAsync(id);
            TempData["SuccessMessage"] = "Sertifika e-postası tekrar gönderildi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            if (!await IsAuthorizedForCertificate(id))
            {
                return Unauthorized("Yetkisiz işlem! Başka bir kuruma ait dosyayı indiremezsiniz.");
            }

            var bytes = await _certificateService.GetCertificateFileAdminAsync(id);
            if (bytes == null) return NotFound("Sertifika dosyası bulunamadı.");

            return File(bytes, "application/pdf", $"certificate_{id}.pdf");
        }
    }
}