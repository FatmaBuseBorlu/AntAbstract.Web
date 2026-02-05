using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
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

        public CertificatesController(AppDbContext context, ICertificateService certificateService)
        {
            _context = context;
            _certificateService = certificateService;
        }

        // Admin listesi: tüm sertifikalar (filtrelenebilir)
        public async Task<IActionResult> Index(
            Guid? conferenceId = null,
            string? userEmail = null,
            CertificateType? type = null,
            bool onlyMissingFile = false,
            bool onlyEmailNotSent = false)
        {
            var q = _context.Certificates
                .AsNoTracking()
                .Include(x => x.Conference)
                .Include(x => x.User)
                .AsQueryable();

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

        // Admin: dosyayı yeniden üret (force)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Regenerate(Guid id)
        {
            await _certificateService.RegenerateCertificateFileAsync(id, resendEmail: false);
            TempData["SuccessMessage"] = "Sertifika dosyası yeniden üretildi.";
            return RedirectToAction(nameof(Index));
        }

        // Admin: maili tekrar gönder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmail(Guid id)
        {
            await _certificateService.ResendCertificateEmailAsync(id);
            TempData["SuccessMessage"] = "Sertifika e-postası tekrar gönderildi.";
            return RedirectToAction(nameof(Index));
        }

        // Admin: dosyayı indir (admin override)
        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            var bytes = await _certificateService.GetCertificateFileAdminAsync(id);
            if (bytes == null) return NotFound("Sertifika dosyası bulunamadı.");

            return File(bytes, "application/pdf", $"certificate_{id}.pdf");
        }
    }
}
