using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ConferencesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public ConferencesController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/Conferences")]
        public async Task<IActionResult> SelectConference()
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var selectedConf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", selectedConf.Tenant.Slug);
                    return Redirect($"/{selectedConf.Tenant.Slug}/Admin/Conferences");
                }
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Kongre Tanımları",
                Lead = "Kongre tanımlarını yönetmek için önce bir kongre seçin.",
                PostUrl = "/Admin/Conferences/Select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Conferences/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId)
        {
            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return Redirect("/Admin/Conferences");
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Admin/Conferences");
        }

        [HttpGet("/{slug}/Admin/Conferences")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null)
                return Redirect("/Admin/Conferences");

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Conferences");

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Where(c => c.TenantId == _tenantContext.Current.Id)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(conferences);
        }

        [HttpGet("/{slug}/Admin/Conferences/Create")]
        public IActionResult Create(string slug)
        {
            if (_tenantContext.Current == null)
                return Redirect("/Admin/Conferences");

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Conferences");

            return View();
        }

        [HttpPost("/{slug}/Admin/Conferences/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Conference conference)
        {
            if (_tenantContext.Current == null)
                return Redirect("/Admin/Conferences");

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Conferences");

            if (!ModelState.IsValid)
                return View(conference);

            conference.Id = Guid.NewGuid();
            conference.TenantId = _tenantContext.Current.Id;

            _context.Conferences.Add(conference);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kongre başarıyla oluşturuldu.";
            return Redirect($"/{slug}/Admin/Conferences");
        }

        [HttpGet("/{slug}/Admin/Conferences/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id)
        {
            if (_tenantContext.Current == null)
                return Redirect("/Admin/Conferences");

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Conferences");

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
            if (_tenantContext.Current == null)
                return Redirect("/Admin/Conferences");

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Conferences");

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
            if (_tenantContext.Current == null)
                return Redirect("/Admin/Conferences");

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect("/Admin/Conferences");

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
