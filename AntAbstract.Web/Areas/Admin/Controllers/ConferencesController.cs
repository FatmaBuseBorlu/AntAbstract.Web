using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ConferencesController : Controller
    {
        private const long MaxTemplateFileSize = 10 * 1024 * 1024;

        private static readonly HashSet<string> AllowedTemplateExtensions = new(
            new[] { ".pdf", ".doc", ".docx" },
            StringComparer.OrdinalIgnoreCase
        );

        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<ConferencesController> _localizer;

        public ConferencesController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IStringLocalizer<ConferencesController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _env = env;
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

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            if (_tenantContext.Current == null)
            {
                return false;
            }

            if (User.IsInRole("SuperAdmin"))
            {
                return true;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return false;
            }

            return tenantId.Value == _tenantContext.Current.Id;
        }

        private async Task FillTenantViewBagAsync(Guid? selectedTenantId = null)
        {
            if (User.IsInRole("SuperAdmin"))
            {
                var allTenants = await _context.Tenants
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .ToListAsync();

                ViewBag.Tenants = new SelectList(
                    allTenants,
                    "Id",
                    "Name",
                    selectedTenantId);

                return;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                ViewBag.Tenants = new SelectList(new List<Tenant>(), "Id", "Name");
                return;
            }

            var tenants = await _context.Tenants
                .AsNoTracking()
                .Where(x => x.Id == tenantId.Value)
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewBag.Tenants = new SelectList(
                tenants,
                "Id",
                "Name",
                selectedTenantId ?? tenantId.Value);
        }

        private static string GenerateSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "conference";
            }

            var text = title.Trim().ToLowerInvariant();

            text = text
                .Replace("ş", "s")
                .Replace("ı", "i")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c");

            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            text = Regex.Replace(text, @"\s+", "-").Trim('-');

            return string.IsNullOrWhiteSpace(text)
                ? "conference"
                : text;
        }

        private async Task<string> GenerateUniqueConferenceSlugAsync(
            string title,
            Guid? ignoreConferenceId = null)
        {
            var baseSlug = GenerateSlug(title);
            var candidate = baseSlug;
            var counter = 2;

            while (await _context.Conferences
                       .AsNoTracking()
                       .AnyAsync(c =>
                           c.Slug == candidate &&
                           (!ignoreConferenceId.HasValue || c.Id != ignoreConferenceId.Value)))
            {
                candidate = $"{baseSlug}-{counter}";
                counter++;
            }

            return candidate;
        }

        private void RemoveConferenceNavigationModelState()
        {
            ModelState.Remove("Tenant");
            ModelState.Remove("Slug");
            ModelState.Remove("Registrations");
            ModelState.Remove("ConferencePageBlocks");
            ModelState.Remove("Submissions");
            ModelState.Remove("ReviewAssignments");
            ModelState.Remove("Sessions");
        }

        private void ValidateConferenceDates(Conference conference)
        {
            if (conference.StartDate != default &&
                conference.EndDate != default &&
                conference.EndDate < conference.StartDate)
            {
                ModelState.AddModelError(
                    nameof(conference.EndDate),
                    T("Error_EndDateBeforeStartDate", "Bitiş tarihi başlangıç tarihinden önce olamaz."));
            }
        }

        private async Task<string> UploadTemplateFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException(
                    T("Error_FileRequired", "Dosya seçilmedi."));
            }

            if (file.Length > MaxTemplateFileSize)
            {
                throw new InvalidOperationException(
                    T("Error_FileTooLarge", "Dosya boyutu en fazla 10 MB olabilir."));
            }

            var extension = Path.GetExtension(file.FileName);

            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedTemplateExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    T("Error_InvalidTemplateFileExtension", "Sadece PDF, DOC ve DOCX dosyaları yüklenebilir."));
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "templates");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
            var absolutePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using var fileStream = new FileStream(absolutePath, FileMode.Create);
            await file.CopyToAsync(fileStream);

            return "/uploads/templates/" + uniqueFileName;
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? _tenantContext.Current?.Slug ?? "";
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        [HttpGet("/Admin/Conferences")]
        public async Task<IActionResult> RootIndex()
        {
            if (User.IsInRole("SuperAdmin"))
            {
                return Redirect("/Admin/AllConferences");
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == tenantId.Value);

            if (tenant != null && !string.IsNullOrWhiteSpace(tenant.Slug))
            {
                return Redirect($"/{tenant.Slug}/Admin/Conferences");
            }

            TempData["ErrorMessage"] = T(
                "Error_TenantNotFound",
                "Hesabınıza bağlı kurum bulunamadı.");

            return Redirect("/Dashboard/MyConferences");
        }

        [HttpGet("/{slug}/Admin/Conferences")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreleri görüntüleme yetkiniz yok.");

                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Where(c => c.TenantId == _tenantContext.Current.Id)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            ViewBag.IsSuperAdmin = User.IsInRole("SuperAdmin");
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(conferences);
        }

        [HttpGet("/{slug}/Admin/Conferences/Create")]
        public async Task<IActionResult> Create(string slug)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_CreatePermission",
                    "Bu kurum için kongre oluşturma yetkiniz yok.");

                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            await FillTenantViewBagAsync(_tenantContext.Current.Id);

            return View(new Conference
            {
                TenantId = _tenantContext.Current.Id,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today
            });
        }

        [HttpPost("/{slug}/Admin/Conferences/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Conference conference)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_CreatePermission",
                    "Bu kurum için kongre oluşturma yetkiniz yok.");

                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            RemoveConferenceNavigationModelState();

            conference.TenantId = _tenantContext.Current.Id;

            ValidateConferenceDates(conference);

            if (!ModelState.IsValid)
            {
                await FillTenantViewBagAsync(conference.TenantId);
                return View(conference);
            }

            conference.Id = Guid.NewGuid();
            conference.Slug = await GenerateUniqueConferenceSlugAsync(conference.Title);

            _context.Conferences.Add(conference);
            await _context.SaveChangesAsync();

            var savedConference = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == conference.Id);

            if (savedConference != null)
            {
                SetSelectedConferenceSession(savedConference);
            }

            TempData["SuccessMessage"] = T(
                "Success_ConferenceCreated",
                "Kongre başarıyla oluşturuldu.");

            return Redirect($"/{slug}/Admin/Conferences");
        }

        [HttpGet("/{slug}/Admin/Conferences/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreyi düzenleme yetkiniz yok.");

                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return NotFound();
            }

            await FillTenantViewBagAsync(conference.TenantId);

            return View(conference);
        }

        [HttpPost("/{slug}/Admin/Conferences/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string slug,
            Guid id,
            Conference conference,
            IFormFile? WritingRulesFile,
            IFormFile? AbstractTemplateFile,
            IFormFile? FullTextTemplateFile)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreyi güncelleme yetkiniz yok.");

                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (id != conference.Id)
            {
                return NotFound();
            }

            RemoveConferenceNavigationModelState();
            ValidateConferenceDates(conference);

            if (!ModelState.IsValid)
            {
                conference.TenantId = _tenantContext.Current.Id;
                await FillTenantViewBagAsync(conference.TenantId);
                return View(conference);
            }

            var existingConference = await _context.Conferences
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current.Id);

            if (existingConference == null)
            {
                return NotFound();
            }

            try
            {
                if (WritingRulesFile != null && WritingRulesFile.Length > 0)
                {
                    existingConference.WritingRulesPath = await UploadTemplateFileAsync(WritingRulesFile);
                }

                if (AbstractTemplateFile != null && AbstractTemplateFile.Length > 0)
                {
                    existingConference.AbstractTemplatePath = await UploadTemplateFileAsync(AbstractTemplateFile);
                }

                if (FullTextTemplateFile != null && FullTextTemplateFile.Length > 0)
                {
                    existingConference.FullTextTemplatePath = await UploadTemplateFileAsync(FullTextTemplateFile);
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);

                conference.TenantId = existingConference.TenantId;
                conference.WritingRulesPath = existingConference.WritingRulesPath;
                conference.AbstractTemplatePath = existingConference.AbstractTemplatePath;
                conference.FullTextTemplatePath = existingConference.FullTextTemplatePath;

                await FillTenantViewBagAsync(conference.TenantId);
                return View(conference);
            }

            existingConference.Title = conference.Title;
            existingConference.StartDate = conference.StartDate;
            existingConference.EndDate = conference.EndDate;
            existingConference.Description = conference.Description;
            existingConference.Venue = conference.Venue;
            existingConference.TenantId = _tenantContext.Current.Id;

            existingConference.CertificateFirstSignerName = conference.CertificateFirstSignerName;
            existingConference.CertificateFirstSignerTitle = conference.CertificateFirstSignerTitle;
            existingConference.CertificateSecondSignerName = conference.CertificateSecondSignerName;
            existingConference.CertificateSecondSignerTitle = conference.CertificateSecondSignerTitle;

            existingConference.AbstractSubmissionDeadline = conference.AbstractSubmissionDeadline;
            existingConference.FullTextSubmissionDeadline = conference.FullTextSubmissionDeadline;
            existingConference.IsSubmissionOpen = conference.IsSubmissionOpen;
            existingConference.MaxRegistrations = conference.MaxRegistrations;
            existingConference.IsRegistrationOpen = conference.IsRegistrationOpen;

            if (!string.IsNullOrWhiteSpace(existingConference.Title))
            {
                existingConference.Slug = await GenerateUniqueConferenceSlugAsync(
                    existingConference.Title,
                    existingConference.Id);
            }

            await _context.SaveChangesAsync();

            var savedConference = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == existingConference.Id);

            if (savedConference != null)
            {
                SetSelectedConferenceSession(savedConference);
            }

            TempData["SuccessMessage"] = T(
                "Success_ConferenceUpdated",
                "Kongre başarıyla güncellendi.");

            return Redirect(User.IsInRole("SuperAdmin")
                ? "/Admin/AllConferences"
                : $"/{slug}/Admin/Conferences");
        }

        [HttpPost("/{slug}/Admin/Conferences/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_DeletePermission",
                    "Bu kongreyi silme yetkiniz yok.");

                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : $"/{slug}/Admin/Conferences");
            }

            var hasRelatedData =
                await _context.Registrations.AnyAsync(x => x.ConferenceId == conference.Id) ||
                await _context.Submissions.AnyAsync(x => x.ConferenceId == conference.Id) ||
                await _context.Sessions.AnyAsync(x => x.ConferenceId == conference.Id) ||
                await _context.ConferencePageBlocks.AnyAsync(x => x.ConferenceId == conference.Id) ||
                await _context.ReviewAssignments
                    .Include(x => x.Submission)
                    .AnyAsync(x =>
                        x.Submission != null &&
                        x.Submission.ConferenceId == conference.Id);

            if (hasRelatedData)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceHasRelatedData",
                    "Bu kongreye bağlı kayıt, bildiri, oturum, website bloğu veya hakem değerlendirmesi olduğu için silinemez.");

                return Redirect(User.IsInRole("SuperAdmin")
                    ? "/Admin/AllConferences"
                    : $"/{slug}/Admin/Conferences");
            }

            _context.Conferences.Remove(conference);
            await _context.SaveChangesAsync();

            _selectedConferenceService.ClearSelectedConferenceId();

            TempData["SuccessMessage"] = T(
                "Success_ConferenceDeleted",
                "Kongre başarıyla silindi.");

            return Redirect(User.IsInRole("SuperAdmin")
                ? "/Admin/AllConferences"
                : $"/{slug}/Admin/Conferences");
        }
    }
}