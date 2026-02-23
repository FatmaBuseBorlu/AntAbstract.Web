using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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

        public ConferencesController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
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

            if (!ModelState.IsValid)
                return View(conference);

            conference.Id = Guid.NewGuid();
            conference.TenantId = _tenantContext.Current.Id;

            _context.Conferences.Add(conference);
            await _context.SaveChangesAsync();

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", _tenantContext.Current.Slug);

            TempData["SuccessMessage"] = "Kongre başarıyla oluşturuldu.";
            return Redirect($"/{slug}/Admin/Conferences");
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
        public async Task<IActionResult> Edit(string slug, Guid id, Conference conference)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Dashboard");

            if (id != conference.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(conference);

            var existingConf = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.Current.Id);

            if (existingConf == null)
                return NotFound();

            existingConf.Title = conference.Title;
            existingConf.StartDate = conference.StartDate;
            existingConf.EndDate = conference.EndDate;
            existingConf.Description = conference.Description;
            existingConf.Venue = conference.Venue;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Kongre bilgileri güncellendi.";

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