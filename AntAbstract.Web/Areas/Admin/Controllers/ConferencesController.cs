using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting; // YENİ EKLENDİ
using Microsoft.AspNetCore.Http; // YENİ EKLENDİ
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO; // YENİ EKLENDİ
using System.Linq;
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

        public ConferencesController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env) 
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _env = env;
        }

        [HttpGet("/Admin/Conferences")]
        public async Task<IActionResult> RootIndex()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin && user?.TenantId != null)
            {
                var tenant = await _context.FindAsync<Tenant>(user.TenantId.Value);
                if (tenant != null && !string.IsNullOrWhiteSpace(tenant.Slug))
                {
                    return Redirect($"/{tenant.Slug}/Admin/Conferences");
                }
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var conf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    return Redirect($"/{conf.Tenant.Slug}/Admin/Conferences");
                }
            }

            TempData["ErrorMessage"] = "Lütfen işlem yapmak istediğiniz bir kongreyi ana ekrandan seçin.";
            return Redirect("/Admin/Dashboard");
        }

        [HttpGet("/{slug}/Admin/Conferences")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("/Admin/Dashboard");
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Where(c => c.TenantId == _tenantContext.Current.Id)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            ViewBag.IsSuperAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            return View(conferences);
        }

        [HttpGet("/{slug}/Admin/Conferences/Create")]
        public async Task<IActionResult> Create(string slug)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Dashboard");

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Yeni kongre oluşturma yetkisi sadece Sistem Yöneticisine aittir. Lütfen destek talebi oluşturun.";
                return Redirect($"/{slug}/Admin/Conferences");
            }

            var tenants = await _context.Tenants.ToListAsync();
            ViewBag.Tenants = new SelectList(tenants, "Id", "Name", _tenantContext.Current.Id);

            return View();
        }

        [HttpPost("/{slug}/Admin/Conferences/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Conference conference)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Dashboard");

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Yeni kongre oluşturma yetkisi sadece Sistem Yöneticisine aittir.";
                return Redirect($"/{slug}/Admin/Conferences");
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
                var tenants = await _context.Tenants.ToListAsync();
                ViewBag.Tenants = new SelectList(tenants, "Id", "Name", conference.TenantId);
                return View(conference);
            }

            conference.Id = Guid.NewGuid();

            if (conference.TenantId == Guid.Empty)
            {
                conference.TenantId = _tenantContext.Current.Id;
            }

            if (string.IsNullOrEmpty(conference.Slug) && !string.IsNullOrEmpty(conference.Title))
            {
                string text = conference.Title.ToLowerInvariant();
                text = text.Replace("ş", "s").Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ö", "o").Replace("ç", "c");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[^a-z0-9\s-]", "");
                conference.Slug = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", "-").Trim('-');
            }

            _context.Conferences.Add(conference);
            await _context.SaveChangesAsync();

            var assignedTenant = await _context.Tenants.FindAsync(conference.TenantId);
            var redirectSlug = assignedTenant?.Slug ?? slug;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", redirectSlug);

            TempData["SuccessMessage"] = "Harika! Kongre başarıyla oluşturuldu ve ilgili kuruma atandı.";
            return Redirect($"/{redirectSlug}/Admin/Conferences");
        }

        [HttpGet("/{slug}/Admin/Conferences/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Dashboard");

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
                return NotFound();

            return View(conference);
        }

        [HttpPost("/{slug}/Admin/Conferences/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string slug, Guid id, Conference conference, IFormFile? WritingRulesFile, IFormFile? AbstractTemplateFile, IFormFile? FullTextTemplateFile)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Dashboard");

            if (id != conference.Id)
                return NotFound();

            ModelState.Remove("Tenant");
            ModelState.Remove("Slug");
            ModelState.Remove("Registrations");
            ModelState.Remove("ConferencePageBlocks");
            ModelState.Remove("Submissions");
            ModelState.Remove("ReviewAssignments");
            ModelState.Remove("Sessions");

            if (!ModelState.IsValid)
                return View(conference);

            var existingConf = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.Current.Id);

            if (existingConf == null)
                return NotFound();

            
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "templates");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (WritingRulesFile != null && WritingRulesFile.Length > 0)
            {
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + WritingRulesFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await WritingRulesFile.CopyToAsync(fileStream); }
                existingConf.WritingRulesPath = "/uploads/templates/" + uniqueFileName;
            }

            if (AbstractTemplateFile != null && AbstractTemplateFile.Length > 0)
            {
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + AbstractTemplateFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await AbstractTemplateFile.CopyToAsync(fileStream); }
                existingConf.AbstractTemplatePath = "/uploads/templates/" + uniqueFileName;
            }

            if (FullTextTemplateFile != null && FullTextTemplateFile.Length > 0)
            {
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + FullTextTemplateFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await FullTextTemplateFile.CopyToAsync(fileStream); }
                existingConf.FullTextTemplatePath = "/uploads/templates/" + uniqueFileName;
            }

            existingConf.Title = conference.Title;
            existingConf.StartDate = conference.StartDate;
            existingConf.EndDate = conference.EndDate;
            existingConf.Description = conference.Description;
            existingConf.Venue = conference.Venue;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Kongre bilgileri ve dosyalar başarıyla güncellendi.";

            return Redirect($"/{slug}/Admin/Conferences");
        }

        [HttpPost("/{slug}/Admin/Conferences/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Dashboard");

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                TempData["ErrorMessage"] = "Kongre silme yetkisi sadece Sistem Yöneticisine aittir.";
                return Redirect($"/{slug}/Admin/Conferences");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.Current.Id);

            if (conference != null)
            {
                _context.Conferences.Remove(conference);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Kongre silindi.";
            }

            return Redirect($"/{slug}/Admin/Conferences");
        }
    }
}