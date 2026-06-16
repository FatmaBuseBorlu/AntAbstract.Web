using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Files;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
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
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<ConferencesController> _localizer;
        private readonly IUploadFileValidator _uploadFileValidator;

        public ConferencesController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IAdminTenantAccessService tenantAccess,
            IWebHostEnvironment env,
            IStringLocalizer<ConferencesController> localizer,
            IUploadFileValidator uploadFileValidator)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _tenantAccess = tenantAccess;
            _env = env;
            _localizer = localizer;
            _uploadFileValidator = uploadFileValidator;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private bool IsSuperAdminUser()
        {
            return _tenantAccess.IsSuperAdmin(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            return await _tenantAccess.CanAccessCurrentTenantAsync(User);
        }

        private async Task FillTenantViewBagAsync(Guid? selectedTenantId = null)
        {
            if (IsSuperAdminUser())
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
            var validation = await _uploadFileValidator.ValidateAsync(
                file,
                UploadFileProfile.ConferenceTemplate);

            if (!validation.IsValid)
            {
                var errorMessage = validation.Error switch
                {
                    UploadValidationError.Empty => T(
                        "Error_FileRequired",
                        "Dosya seçilmedi."),
                    UploadValidationError.TooLarge => T(
                        "Error_FileTooLarge",
                        "Dosya boyutu en fazla 10 MB olabilir."),
                    UploadValidationError.InvalidExtension => T(
                        "Error_InvalidTemplateFileExtension",
                        "Sadece PDF, DOC ve DOCX dosyaları yüklenebilir."),
                    _ => T(
                        "Error_InvalidTemplateFileContent",
                        "Dosya içeriği seçilen formatla eşleşmiyor.")
                };

                throw new InvalidOperationException(errorMessage);
            }

            var uploadsFolder = PrivateStorage.EnsureFolder(_env, PrivateStorage.TemplatesFolder);

            var uniqueFileName = _uploadFileValidator.CreateStoredFileName(
                validation.Extension,
                "template");
            var absolutePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using var fileStream = new FileStream(absolutePath, FileMode.Create);
            await file.CopyToAsync(fileStream);

            return PrivateStorage.ToRelativePath(PrivateStorage.TemplatesFolder, uniqueFileName);
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
            if (IsSuperAdminUser())
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
                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreleri görüntüleme yetkiniz yok.");

                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Where(c => c.TenantId == _tenantContext.Current.Id)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            ViewBag.IsSuperAdmin = IsSuperAdminUser();
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(conferences);
        }

        [HttpGet("/{slug}/Admin/Conferences/Create")]
        public async Task<IActionResult> Create(string slug)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_CreatePermission",
                    "Bu kurum için kongre oluşturma yetkiniz yok.");

                return Redirect(IsSuperAdminUser()
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
                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_CreatePermission",
                    "Bu kurum için kongre oluşturma yetkiniz yok.");

                return Redirect(IsSuperAdminUser()
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
                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreyi düzenleme yetkiniz yok.");

                return Redirect(IsSuperAdminUser()
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
                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreyi güncelleme yetkiniz yok.");

                return Redirect(IsSuperAdminUser()
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

            return Redirect(IsSuperAdminUser()
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
                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_DeletePermission",
                    "Bu kongreyi silme yetkiniz yok.");

                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : "/Dashboard/MyConferences");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return Redirect(IsSuperAdminUser()
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

                return Redirect(IsSuperAdminUser()
                    ? "/Admin/AllConferences"
                    : $"/{slug}/Admin/Conferences");
            }

            _context.Conferences.Remove(conference);
            await _context.SaveChangesAsync();

            _selectedConferenceService.ClearSelectedConferenceId();

            TempData["SuccessMessage"] = T(
                "Success_ConferenceDeleted",
                "Kongre başarıyla silindi.");

            return Redirect(IsSuperAdminUser()
                ? "/Admin/AllConferences"
                : $"/{slug}/Admin/Conferences");
        }

        // ── Konferans Klonlama ────────────────────────────────────────────────────

        [HttpPost("/{slug}/Admin/Conferences/Clone/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clone(string slug, Guid id)
        {
            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T("Error_ClonePermission", "Bu işlem için yetkiniz yok.");
                return Redirect($"/{slug}/Admin/Conferences");
            }

            var source = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current!.Id);

            if (source == null)
            {
                TempData["ErrorMessage"] = T("Error_ConferenceNotFound", "Kongre bulunamadı.");
                return Redirect($"/{slug}/Admin/Conferences");
            }

            // Yeni kongre — tüm temel alanları kopyala, tarihler/başlık sıfırlanır
            var clone = new Conference
            {
                Id = Guid.NewGuid(),
                TenantId = source.TenantId,
                Title = source.Title + " (Kopya)",
                Description = source.Description,
                City = source.City,
                Country = source.Country,
                Venue = source.Venue,
                StartDate = source.StartDate,
                EndDate = source.EndDate,
                IsSubmissionOpen = false,       // Kopyada kapalı başlar
                IsRegistrationOpen = false,
                MaxRegistrations = source.MaxRegistrations,
                IsProceedingBookPublished = false,
                CertificateFirstSignerName = source.CertificateFirstSignerName,
                CertificateFirstSignerTitle = source.CertificateFirstSignerTitle,
                CertificateSecondSignerName = source.CertificateSecondSignerName,
                CertificateSecondSignerTitle = source.CertificateSecondSignerTitle,
            };

            clone.Slug = await GenerateUniqueConferenceSlugAsync(clone.Title, clone.Id);

            _context.Conferences.Add(clone);

            // Konuları kopyala
            var topics = await _context.ConferenceTopics
                .AsNoTracking()
                .Where(t => t.ConferenceId == id)
                .ToListAsync();

            foreach (var topic in topics)
            {
                _context.ConferenceTopics.Add(new ConferenceTopic
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = clone.Id,
                    Name = topic.Name,
                    IsActive = topic.IsActive
                });
            }

            // Kayıt türlerini kopyala
            var regTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(r => r.ConferenceId == id)
                .ToListAsync();

            foreach (var rt in regTypes)
            {
                _context.RegistrationTypes.Add(new RegistrationType
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = clone.Id,
                    Name = rt.Name,
                    Price = rt.Price,
                    IsActive = rt.IsActive,
                    Deadline = rt.Deadline,
                    RoleName = rt.RoleName
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"\"{source.Title}\" kongresi başarıyla kopyalandı. Yeni kongre: \"{clone.Title}\"";

            return Redirect($"/{slug}/Admin/Conferences");
        }
    }
}