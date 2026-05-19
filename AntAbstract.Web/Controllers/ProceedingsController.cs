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

namespace AntAbstract.Web.Controllers
{
    public class ProceedingsController : Controller
    {
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
                    DownloadUrl = $"/Proceedings/Download/{c.Id}",
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
        public async Task<IActionResult> Index(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction(nameof(IndexRoot));
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Bildiri kitabı görüntülenecek kongre bulunamadı.";
                return RedirectToAction(nameof(IndexRoot));
            }

            var model = new ProceedingBookPageViewModel
            {
                ConferenceId = conference.Id,
                Slug = slug,
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
                    Slug = slug,
                    FileUrl = NormalizeFileUrl(conference.ProceedingBookFilePath),
                    DownloadUrl = $"/Proceedings/Download/{conference.Id}",
                    Year = conference.StartDate.Year,
                    PublishedDate = conference.ProceedingBookPublishedDate,
                    StatusText = "Yayında",
                    CategoryText = "Bildiri Kitabı"
                });
            }

            return View("~/Views/Proceedings/Index.cshtml", model);
        }

        [HttpGet("/Proceedings/Download/{conferenceId:guid}")]
        public async Task<IActionResult> Download(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return BadRequest("Geçersiz bildiri kitabı isteği.");
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Id == conferenceId &&
                    c.IsProceedingBookPublished &&
                    !string.IsNullOrWhiteSpace(c.ProceedingBookFilePath));

            if (conference == null)
            {
                return NotFound("Bildiri kitabı bulunamadı veya yayında değil.");
            }

            var filePath = conference.ProceedingBookFilePath ?? "";

            if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(filePath);
            }

            var normalizedPath = filePath.TrimStart('/', '\\');
            var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, normalizedPath);

            if (!System.IO.File.Exists(physicalPath))
            {
                return NotFound("PDF dosyası sunucuda bulunamadı.");
            }

            var slug = conference.Tenant?.Slug ?? conference.Slug ?? "bildiri-kitabi";
            var safeFileName = $"{slug}-bildiri-kitabi.pdf";

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