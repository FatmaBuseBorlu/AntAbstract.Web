using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Proceedings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
namespace AntAbstract.Web.Controllers
{
    public class ProceedingsController : Controller
    {
        // Kurucu metoda dokunmadan çeviri: mesajlar eskiden doğrudan Türkçe
        // yazılıydı, İngilizce seçili kullanıcıya da Türkçe dönüyorlardı.
        private string T(string key, string fallback)
        {
            var value = HttpContext?.RequestServices
                .GetService<IStringLocalizer<ProceedingsController>>()?[key];

            return value == null || value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProceedingsController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet("/Proceedings/Index")]
        public async Task<IActionResult> IndexRoot()
        {
            var books = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c =>
                    c.IsProceedingBookPublished &&
                    !string.IsNullOrWhiteSpace(c.ProceedingBookFilePath))
                .OrderByDescending(c => c.ProceedingBookPublishedDate ?? c.EndDate)
                .Select(c => new ProceedingBookItemViewModel
                {
                    ConferenceId = c.Id,
                    ConferenceTitle = c.Title ?? "",
                    Slug = c.Tenant != null && !string.IsNullOrWhiteSpace(c.Tenant.Slug)
                        ? c.Tenant.Slug
                        : (c.Slug ?? ""),
                    FileUrl = NormalizeFileUrl(c.ProceedingBookFilePath),
                    DownloadUrl = c.Tenant != null && !string.IsNullOrWhiteSpace(c.Tenant.Slug)
                        ? $"/{c.Tenant.Slug}/Proceedings/Download/{c.Id}"
                        : $"/Proceedings/Download/{c.Id}",
                    Year = c.StartDate.Year,
                    PublishedDate = c.ProceedingBookPublishedDate,
                    StatusText = "Yayında",
                    CategoryText = "Bildiri Kitabı"
                })
                .ToListAsync();

            var model = new ProceedingBookPageViewModel
            {
                IsSingleConferencePage = false,
                Books = books
            };

            return View("~/Views/Proceedings/Index.cshtml", model);
        }

        [HttpGet("/{slug}/Proceedings")]
        [HttpGet("/{slug}/Proceedings/Index")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction(nameof(IndexRoot));
            }

            var query = _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            var conference = conferenceId.HasValue && conferenceId.Value != Guid.Empty
                ? await query.FirstOrDefaultAsync(c =>
                    c.Id == conferenceId.Value &&
                    (
                        c.Slug == slug ||
                        (c.Tenant != null && c.Tenant.Slug == slug)
                    ))
                : await query
                    .Where(c =>
                        c.Slug == slug ||
                        (c.Tenant != null && c.Tenant.Slug == slug))
                    .OrderByDescending(c => c.IsProceedingBookPublished)
                    .ThenByDescending(c => c.ProceedingBookPublishedDate ?? c.EndDate)
                    .FirstOrDefaultAsync();

            if (conference == null)
            {
                TempData["ErrorMessage"] = T("Msg_BildiriKitabiGoruntulenecekKongreBulunamadi", "Bildiri kitabı görüntülenecek kongre bulunamadı.");
                return RedirectToAction(nameof(IndexRoot));
            }

            var resolvedSlug = conference.Tenant != null && !string.IsNullOrWhiteSpace(conference.Tenant.Slug)
                ? conference.Tenant.Slug
                : (!string.IsNullOrWhiteSpace(conference.Slug) ? conference.Slug : slug);

            var model = new ProceedingBookPageViewModel
            {
                ConferenceId = conference.Id,
                Slug = resolvedSlug,
                ConferenceTitle = conference.Title ?? "",
                ProceedingBookFilePath = conference.ProceedingBookFilePath,
                IsProceedingBookPublished = conference.IsProceedingBookPublished,
                ProceedingBookPublishedDate = conference.ProceedingBookPublishedDate,
                IsSingleConferencePage = true
            };

            if (conference.IsProceedingBookPublished &&
                !string.IsNullOrWhiteSpace(conference.ProceedingBookFilePath))
            {
                model.Books.Add(new ProceedingBookItemViewModel
                {
                    ConferenceId = conference.Id,
                    ConferenceTitle = conference.Title ?? "",
                    Slug = resolvedSlug,
                    FileUrl = NormalizeFileUrl(conference.ProceedingBookFilePath),
                    DownloadUrl = !string.IsNullOrWhiteSpace(resolvedSlug)
                        ? $"/{resolvedSlug}/Proceedings/Download/{conference.Id}"
                        : $"/Proceedings/Download/{conference.Id}",
                    Year = conference.StartDate.Year,
                    PublishedDate = conference.ProceedingBookPublishedDate,
                    StatusText = "Yayında",
                    CategoryText = "Bildiri Kitabı"
                });
            }

            return View("~/Views/Proceedings/Index.cshtml", model);
        }

        [HttpGet("/Proceedings/Submission/{code}")]
        [HttpGet("/{slug}/Proceedings/Submission/{code}")]
        public async Task<IActionResult> Submission(string code, string? slug = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return NotFound("Bildiri bulunamadı.");
            }

            var normalizedCode = code.Trim();

            var query = _context.Submissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(s => s.SubmissionAuthors)
                .Where(s =>
                    s.SubmissionIdCode == normalizedCode &&
                    (s.Status == SubmissionStatus.Accepted || s.Status == SubmissionStatus.Presented));

            if (!string.IsNullOrWhiteSpace(slug))
            {
                query = query.Where(s =>
                    s.Conference != null &&
                    (
                        s.Conference.Slug == slug ||
                        (s.Conference.Tenant != null && s.Conference.Tenant.Slug == slug)
                    ));
            }

            var submission = await query.FirstOrDefaultAsync();
            if (submission == null)
            {
                return NotFound("Bildiri bulunamadı veya henüz yayında değil.");
            }

            var resolvedSlug = submission.Conference?.Tenant?.Slug
                ?? submission.Conference?.Slug
                ?? slug
                ?? "";

            var model = new ProceedingSubmissionViewModel
            {
                Slug = resolvedSlug,
                SubmissionIdCode = submission.SubmissionIdCode ?? "",
                Title = submission.Title ?? "",
                Abstract = submission.Abstract ?? "",
                Keywords = submission.Keywords ?? "",
                Topic = submission.Topic ?? "",
                PresentationType = submission.PresentationType ?? "",
                ConferenceTitle = submission.Conference?.Title ?? "",
                ConferenceStartDate = submission.Conference?.StartDate ?? DateTime.MinValue,
                ConferenceEndDate = submission.Conference?.EndDate ?? DateTime.MinValue,
                DoiUrl = submission.DoiUrl,
                Authors = submission.SubmissionAuthors
                    .OrderBy(a => a.Order)
                    .Select(a => $"{a.FirstName} {a.LastName}".Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList()
            };

            return View("~/Views/Proceedings/Submission.cshtml", model);
        }

        [HttpGet("/Proceedings/Download/{conferenceId:guid}")]
        [HttpGet("/{slug}/Proceedings/Download/{conferenceId:guid}")]
        public async Task<IActionResult> Download(Guid conferenceId, string? slug = null)
        {
            if (conferenceId == Guid.Empty)
            {
                return BadRequest("Geçersiz bildiri kitabı isteği.");
            }

            var query = _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c =>
                    c.Id == conferenceId &&
                    c.IsProceedingBookPublished &&
                    !string.IsNullOrWhiteSpace(c.ProceedingBookFilePath));

            if (!string.IsNullOrWhiteSpace(slug))
            {
                query = query.Where(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));
            }

            var conference = await query.FirstOrDefaultAsync();

            if (conference == null)
            {
                return NotFound("Bildiri kitabı bulunamadı veya yayında değil.");
            }

            var filePath = conference.ProceedingBookFilePath ?? "";

            // Harici URL: yalnızca http/https protokollerine izin ver (javascript: vb. engelle)
            if (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Sadece mutlak HTTP(S) URL'lere yönlendir
                if (!Uri.TryCreate(filePath, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return BadRequest("Geçersiz yönlendirme adresi.");
                }
                return Redirect(filePath);
            }

            // Yerel dosya: path traversal koruması
            var normalizedPath = filePath.TrimStart('/', '\\');
            var wwwroot = Path.GetFullPath(_webHostEnvironment.WebRootPath);
            var physicalPath = Path.GetFullPath(Path.Combine(wwwroot, normalizedPath));

            // Dosya wwwroot dışına çıkmamalı
            if (!physicalPath.StartsWith(wwwroot, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Geçersiz dosya yolu.");
            }

            if (!System.IO.File.Exists(physicalPath))
            {
                return NotFound("PDF dosyası sunucuda bulunamadı.");
            }

            var fileSlug = conference.Tenant != null && !string.IsNullOrWhiteSpace(conference.Tenant.Slug)
                ? conference.Tenant.Slug
                : (!string.IsNullOrWhiteSpace(conference.Slug) ? conference.Slug : "bildiri-kitabi");

            var safeFileName = $"{fileSlug}-bildiri-kitabi.pdf";

            var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return File(bytes, "application/pdf", safeFileName);
        }

        private static string NormalizeFileUrl(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "#";
            }

            if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return filePath;
            }

            if (filePath.StartsWith("/"))
            {
                return filePath;
            }

            return "/" + filePath.TrimStart('/');
        }
    }
}
