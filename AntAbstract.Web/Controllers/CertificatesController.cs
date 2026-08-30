using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private string GetSlug(string? slug = null)
        {
            if (!string.IsNullOrWhiteSpace(slug))
            {
                return slug;
            }

            return RouteData.Values["slug"]?.ToString()
                   ?? HttpContext.Session.GetString("SelectedConferenceSlug")
                   ?? "";
        }

        private static string BuildUrl(string? slug, string path)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return path;
            }

            return $"/{slug}{path}";
        }

        private static string GetCanonicalSlug(Conference? conference, string? fallbackSlug = null)
        {
            return conference?.Tenant?.Slug
                   ?? conference?.Slug
                   ?? fallbackSlug
                   ?? "";
        }

        private static bool SlugMatches(Conference? conference, string? slug)
        {
            if (conference == null || string.IsNullOrWhiteSpace(slug))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(conference.Slug) &&
                string.Equals(conference.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug) &&
                string.Equals(conference.Tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private void SetSelectedConferenceSession(Conference conference, string slug)
        {
            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
        }

        // Bu ekranın adresleri slug taşımayabiliyor (/Certificates). Slug yoksa
        // tenant bağlamı boş kalıyor ve kiracı filtresi aşağıdaki yardımcı
        // sorguların hepsini boşaltıyor: kullanıcı kendi sertifikasını
        // göremiyor, hatta "Kongre bulunamadı." ile dışarı atılıyordu.
        // Kapsamı her sorgunun kendi userId/id koşulu sağlıyor.
        private async Task<Conference?> GetConferenceBySlugAsync(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c =>
                    c.Slug == slug ||
                    (
                        c.Tenant != null &&
                        c.Tenant.Slug == slug
                    ))
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();
        }

        private async Task<Conference?> GetConferenceByIdAsync(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);
        }

        private async Task<bool> HasCompletedConferenceAttendanceAsync(
            string userId,
            Guid conferenceId)
        {
            return await _context.ConferenceAttendances
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.UserId == userId &&
                    x.ConferenceId == conferenceId &&
                    (
                        x.CompletedAt.HasValue ||
                        x.TotalSeconds >= x.RequiredSeconds
                    ));
        }

        private async Task<Dictionary<Guid, string>> GetConferenceTitlesAsync(
            IEnumerable<Guid> conferenceIds)
        {
            var ids = conferenceIds
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return new Dictionary<Guid, string>();
            }

            return await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new
                {
                    c.Id,
                    c.Title
                })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => string.IsNullOrWhiteSpace(x.Title)
                        ? x.Id.ToString()
                        : x.Title);
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

            var currentSlug = GetSlug(slug);
            Conference? currentConference = null;

            if (!string.IsNullOrWhiteSpace(currentSlug))
            {
                currentConference = await GetConferenceBySlugAsync(currentSlug);

                if (currentConference == null)
                {
                    TempData["ErrorMessage"] = T(
                        "ConferenceNotFound",
                        "Kongre bulunamadı.");

                    return Redirect("/Dashboard/MyConferences");
                }

                var canonicalSlug = GetCanonicalSlug(currentConference, currentSlug);

                if (!string.Equals(canonicalSlug, currentSlug, StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(BuildUrl(canonicalSlug, "/Certificates"));
                }

                SetSelectedConferenceSession(currentConference, canonicalSlug);

                currentSlug = canonicalSlug;
            }

            var certificates = await _certificateService.GetMyCertificatesAsync(user.Id);

            certificates ??= new List<Certificate>();

            if (currentConference != null)
            {
                certificates = certificates
                    .Where(c => c.ConferenceId == currentConference.Id)
                    .ToList();
            }

            ViewBag.Slug = currentSlug;
            ViewBag.CurrentConferenceTitle = currentConference?.Title;
            ViewBag.ConferenceTitles = await GetConferenceTitlesAsync(
                certificates.Select(c => c.ConferenceId));

            return View(certificates);
        }

        [HttpGet("/Certificates/Download/{id:guid}")]
        [HttpGet("/{slug}/Certificates/Download/{id:guid}")]
        public async Task<IActionResult> Download(
            Guid id,
            string? slug = null)
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

            // Listeyle aynı sebep: slug'sız adreste kiracı filtresi kullanıcının
            // kendi sertifikasını da eliyordu. Sahiplik koşulu zaten burada.
            var certificate = await _context.Certificates
                .IgnoreQueryFilters()
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

            var conference = await GetConferenceByIdAsync(certificate.ConferenceId);
            var currentSlug = GetSlug(slug);
            var canonicalSlug = GetCanonicalSlug(conference, currentSlug);

            if (!string.IsNullOrWhiteSpace(currentSlug) &&
                conference != null &&
                (
                    !SlugMatches(conference, currentSlug) ||
                    !string.Equals(canonicalSlug, currentSlug, StringComparison.OrdinalIgnoreCase)
                ))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/Certificates/Download/{id}"));
            }

            if (conference != null && !string.IsNullOrWhiteSpace(canonicalSlug))
            {
                SetSelectedConferenceSession(conference, canonicalSlug);
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

                    return Redirect(BuildUrl(canonicalSlug, "/Certificates"));
                }
            }

            var bytes = await _certificateService.GetCertificateFileAsync(
                id,
                user.Id);

            if (bytes == null || bytes.Length == 0)
            {
                await _certificateService.RegenerateCertificateFileAsync(id);

                bytes = await _certificateService.GetCertificateFileAsync(
                    id,
                    user.Id);
            }

            if (bytes == null || bytes.Length == 0)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı veya bu sertifikayı indirme yetkiniz yok."));
            }

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            var contentType = string.IsNullOrWhiteSpace(certificate.ContentType)
                ? "application/pdf"
                : certificate.ContentType;

            var fileName = string.IsNullOrWhiteSpace(certificate.FileName)
                ? $"certificate_{id}.pdf"
                : certificate.FileName;

            return File(
                bytes,
                contentType,
                fileName);
        }
    }
}