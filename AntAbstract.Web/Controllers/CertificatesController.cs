using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class CertificatesController : Controller
    {
        private readonly ICertificateService _certificateService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<CertificatesController> _localizer;
        private readonly AppDbContext _context;

        public CertificatesController(
            ICertificateService certificateService,
            UserManager<AppUser> userManager,
            IStringLocalizer<CertificatesController> localizer,
            AppDbContext context)
        {
            _certificateService = certificateService;
            _userManager = userManager;
            _localizer = localizer;
            _context = context;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<bool> HasCompletedConferenceAttendanceAsync(string userId, Guid conferenceId)
        {
            return await _context.ConferenceAttendances
                .AsNoTracking()
                .AnyAsync(x =>
                    x.UserId == userId &&
                    x.ConferenceId == conferenceId &&
                    (
                        x.CompletedAt.HasValue ||
                        x.TotalSeconds >= x.RequiredSeconds
                    ));
        }

        [HttpGet("/Certificates")]
        [HttpGet("/Certificates/Index")]
        [HttpGet("/{slug}/Certificates")]
        [HttpGet("/{slug}/Certificates/Index")]
        public async Task<IActionResult> Index(string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            ViewBag.Slug = slug;

            var certificates = await _certificateService.GetMyCertificatesAsync(user.Id);

            return View(certificates);
        }

        [HttpGet("/Certificates/Download/{id:guid}")]
        [HttpGet("/{slug}/Certificates/Download/{id:guid}")]
        public async Task<IActionResult> Download(Guid id, string? slug = null)
        {
            if (id == Guid.Empty)
            {
                return BadRequest(T(
                    "Error_InvalidCertificate",
                    "Geçersiz sertifika isteği."));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var certificate = await _context.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == user.Id);

            if (certificate == null)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı veya bu sertifikayı indirme yetkiniz yok."));
            }

            if (certificate.Type == CertificateType.Author)
            {
                var hasCompletedAttendance = await HasCompletedConferenceAttendanceAsync(
                    user.Id,
                    certificate.ConferenceId);

                if (!hasCompletedAttendance)
                {
                    TempData["ErrorMessage"] = T(
                        "CertificateAttendanceRequired",
                        "Katılım belgeniz henüz hazır değil. Belge oluşturulabilmesi için kongre katılımınızın tamamlanması gerekir.");

                    if (!string.IsNullOrWhiteSpace(slug))
                    {
                        return Redirect($"/{slug}/Certificates");
                    }

                    return RedirectToAction(nameof(Index));
                }
            }

            var bytes = await _certificateService.GetCertificateFileAsync(id, user.Id);

            if (bytes == null || bytes.Length == 0)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı veya bu sertifikayı indirme yetkiniz yok."));
            }

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return File(
                bytes,
                "application/pdf",
                $"certificate_{id}.pdf");
        }
    }
}