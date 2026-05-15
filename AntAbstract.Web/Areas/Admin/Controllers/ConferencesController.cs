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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ConferencesController : Controller
    {
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

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        private async Task<bool> IsCurrentUserAdminAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            return user != null &&
                   await _userManager.IsInRoleAsync(user, "Admin");
        }

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            if (_tenantContext.Current == null)
            {
                return false;
            }

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

            return user.TenantId.HasValue &&
                   user.TenantId.Value == _tenantContext.Current.Id;
        }

        private static string GenerateSlug(string title)
        {
            var text = title.ToLowerInvariant();

            text = text
                .Replace("ş", "s")
                .Replace("ı", "i")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c");

            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            text = Regex.Replace(text, @"\s+", "-").Trim('-');

            return text;
        }

        [HttpGet("/Admin/Conferences")]
        public async Task<IActionResult> RootIndex()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin && user?.TenantId != null)
            {
                var tenant = await _context.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == user.TenantId.Value);

                if (tenant != null && !string.IsNullOrWhiteSpace(tenant.Slug))
                {
                    return Redirect($"/{tenant.Slug}/Admin/Conferences");
                }
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId != null)
            {
                var query = _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .AsQueryable();

                if (!isAdmin && user?.TenantId != null)
                {
                    query = query.Where(x => x.TenantId == user.TenantId.Value);
                }
                else if (!isAdmin && user?.TenantId == null)
                {
                    query = query.Where(x => false);
                }

                var conf = await query
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    return Redirect($"/{conf.Tenant.Slug}/Admin/Conferences");
                }
            }

            TempData["ErrorMessage"] = T(
                "Error_SelectConferenceFromDashboard",
                "Lütfen önce dashboard üzerinden geçerli bir kongre seçiniz.");

            return Redirect("/Admin/Dashboard");
        }

        [HttpGet("/{slug}/Admin/Conferences")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/Admin/Dashboard");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreleri görüntüleme yetkiniz yok.");

                return Redirect("/Admin/Dashboard");
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Where(c => c.TenantId == _tenantContext.Current.Id)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            ViewBag.IsSuperAdmin = await IsCurrentUserAdminAsync();

            return View(conferences);
        }

        [HttpGet("/{slug}/Admin/Conferences/Create")]
        public async Task<IActionResult> Create(string slug)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/Admin/Dashboard");
            }

            var isAdmin = await IsCurrentUserAdminAsync();

            if (!isAdmin)
            {
                TempData["ErrorMessage"] = T(
                    "Error_CreatePermissionWithSupport",
                    "Kongre oluşturma yetkiniz yok. Lütfen süper admin ile iletişime geçiniz.");

                return Redirect($"/{slug}/Admin/Conferences");
            }

            var tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewBag.Tenants = new SelectList(
                tenants,
                "Id",
                "Name",
                _tenantContext.Current.Id);

            return View();
        }

        [HttpPost("/{slug}/Admin/Conferences/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Conference conference)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/Admin/Dashboard");
            }

            var isAdmin = await IsCurrentUserAdminAsync();

            if (!isAdmin)
            {
                TempData["ErrorMessage"] = T(
                    "Error_CreatePermission",
                    "Kongre oluşturma yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Conferences");
            }

            ModelState.Remove("Tenant");
            ModelState.Remove("Slug");
            ModelState.Remove("Registrations");
            ModelState.Remove("ConferencePageBlocks");
            ModelState.Remove("Submissions");
            ModelState.Remove("ReviewAssignments");
            ModelState.Remove("Sessions");

            if (conference.TenantId == Guid.Empty)
            {
                conference.TenantId = _tenantContext.Current.Id;
            }

            var tenantExists = await _context.Tenants
                .AsNoTracking()
                .AnyAsync(x => x.Id == conference.TenantId);

            if (!tenantExists)
            {
                ModelState.AddModelError(
                    nameof(conference.TenantId),
                    T("Error_TenantNotFound", "Seçilen kurum bulunamadı."));
            }

            if (!ModelState.IsValid)
            {
                var tenants = await _context.Tenants
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .ToListAsync();

                ViewBag.Tenants = new SelectList(
                    tenants,
                    "Id",
                    "Name",
                    conference.TenantId);

                return View(conference);
            }

            conference.Id = Guid.NewGuid();

            if (string.IsNullOrWhiteSpace(conference.Slug) &&
                !string.IsNullOrWhiteSpace(conference.Title))
            {
                conference.Slug = GenerateSlug(conference.Title);
            }

            _context.Conferences.Add(conference);
            await _context.SaveChangesAsync();

            var assignedTenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == conference.TenantId);

            var redirectSlug = assignedTenant?.Slug ?? slug;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", redirectSlug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            TempData["SuccessMessage"] = T(
                "Success_ConferenceCreated",
                "Kongre başarıyla oluşturuldu.");

            return Redirect($"/{redirectSlug}/Admin/Conferences");
        }

        [HttpGet("/{slug}/Admin/Conferences/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/Admin/Dashboard");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreyi düzenleme yetkiniz yok.");

                return Redirect("/Admin/Dashboard");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return NotFound();
            }

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
                return Redirect("/Admin/Dashboard");
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongreyi güncelleme yetkiniz yok.");

                return Redirect("/Admin/Dashboard");
            }

            if (id != conference.Id)
            {
                return NotFound();
            }

            ModelState.Remove("Tenant");
            ModelState.Remove("Slug");
            ModelState.Remove("Registrations");
            ModelState.Remove("ConferencePageBlocks");
            ModelState.Remove("Submissions");
            ModelState.Remove("ReviewAssignments");
            ModelState.Remove("Sessions");

            if (!ModelState.IsValid)
            {
                return View(conference);
            }

            var existingConf = await _context.Conferences
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current.Id);

            if (existingConf == null)
            {
                return NotFound();
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "templates");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (WritingRulesFile != null && WritingRulesFile.Length > 0)
            {
                var uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(WritingRulesFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await WritingRulesFile.CopyToAsync(fileStream);

                existingConf.WritingRulesPath = "/uploads/templates/" + uniqueFileName;
            }

            if (AbstractTemplateFile != null && AbstractTemplateFile.Length > 0)
            {
                var uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(AbstractTemplateFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await AbstractTemplateFile.CopyToAsync(fileStream);

                existingConf.AbstractTemplatePath = "/uploads/templates/" + uniqueFileName;
            }

            if (FullTextTemplateFile != null && FullTextTemplateFile.Length > 0)
            {
                var uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(FullTextTemplateFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await FullTextTemplateFile.CopyToAsync(fileStream);

                existingConf.FullTextTemplatePath = "/uploads/templates/" + uniqueFileName;
            }

            existingConf.Title = conference.Title;
            existingConf.StartDate = conference.StartDate;
            existingConf.EndDate = conference.EndDate;
            existingConf.Description = conference.Description;
            existingConf.Venue = conference.Venue;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_ConferenceUpdated",
                "Kongre başarıyla güncellendi.");

            return Redirect($"/{slug}/Admin/Conferences");
        }

        [HttpPost("/{slug}/Admin/Conferences/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/Admin/Dashboard");
            }

            var isAdmin = await IsCurrentUserAdminAsync();

            if (!isAdmin)
            {
                TempData["ErrorMessage"] = T(
                    "Error_DeletePermission",
                    "Kongre silme yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Conferences");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference != null)
            {
                _context.Conferences.Remove(conference);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = T(
                    "Success_ConferenceDeleted",
                    "Kongre başarıyla silindi.");
            }

            return Redirect($"/{slug}/Admin/Conferences");
        }
    }
}