using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class SponsorsController : Controller
    {
        // Kurucu metoda dokunmadan çeviri: mesajlar eskiden doğrudan Türkçe
        // yazılıydı, İngilizce seçili kullanıcıya da Türkçe dönüyorlardı.
        private string T(string key, string fallback)
        {
            var value = HttpContext?.RequestServices
                .GetService<IStringLocalizer<SponsorsController>>()?[key];

            return value == null || value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private readonly AppDbContext _context;
        private readonly IAdminTenantAccessService _tenantAccess;

        public SponsorsController(AppDbContext context, IAdminTenantAccessService tenantAccess)
        {
            _context = context;
            _tenantAccess = tenantAccess;
        }

        private async Task<Conference?> GetConferenceAsync(string slug, Guid? conferenceId)
        {
            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                var c = await _context.Conferences.Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == conferenceId.Value);
                if (c != null) return c;
            }
            return await _context.Conferences.Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Slug == slug || (x.Tenant != null && x.Tenant.Slug == slug));
        }

        private void SetViewBag(Conference c, string slug)
        {
            ViewBag.Slug = slug;
            ViewBag.ConferenceId = c.Id;
            ViewBag.ConferenceTitle = c.Title;
        }

        // ── Index ────────────────────────────────────────────────────────────

        [HttpGet("/{slug}/Admin/Sponsors")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceAsync(slug, conferenceId);
            if (conference == null) { TempData["ErrorMessage"] = T("Msg_KongreBulunamadi", "Kongre bulunamadı."); return Redirect($"/{slug}/Admin/Reports"); }
            SetViewBag(conference, slug);
            var sponsors = await _context.Sponsors.AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .OrderBy(s => s.Tier).ThenBy(s => s.SortOrder).ThenBy(s => s.Name)
                .ToListAsync();
            return View(sponsors);
        }

        // ── Create ───────────────────────────────────────────────────────────

        [HttpGet("/{slug}/Admin/Sponsors/Create")]
        public async Task<IActionResult> Create(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceAsync(slug, conferenceId);
            if (conference == null) return NotFound();
            SetViewBag(conference, slug);
            return View(new Sponsor { ConferenceId = conference.Id });
        }

        [HttpPost("/{slug}/Admin/Sponsors/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Sponsor model)
        {
            var conference = await GetConferenceAsync(slug, model.ConferenceId != Guid.Empty ? model.ConferenceId : null);
            if (conference == null) return NotFound();
            // Navigasyon alanı formdan gelmez; nullable açık olduğu için MVC
            // bunu zorunlu sayıyor ve doğrulama hiçbir zaman geçmiyordu.
            ModelState.Remove(nameof(Sponsor.Conference));

            if (!ModelState.IsValid) { SetViewBag(conference, slug); return View(model); }

            model.Id = Guid.NewGuid();
            model.ConferenceId = conference.Id;
            model.CreatedAt = DateTime.UtcNow;
            model.LogoUrl = model.LogoUrl?.Trim();
            model.WebsiteUrl = model.WebsiteUrl?.Trim();

            _context.Sponsors.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{model.Name} başarıyla eklendi.";
            return Redirect($"/{slug}/Admin/Sponsors?conferenceId={conference.Id}");
        }

        // ── Edit ─────────────────────────────────────────────────────────────

        [HttpGet("/{slug}/Admin/Sponsors/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id)
        {
            var sponsor = await _context.Sponsors.Include(s => s.Conference).ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (sponsor == null) return NotFound();
            SetViewBag(sponsor.Conference, slug);
            return View(sponsor);
        }

        [HttpPost("/{slug}/Admin/Sponsors/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string slug, Guid id, Sponsor model)
        {
            var existing = await _context.Sponsors.Include(s => s.Conference).ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null) return NotFound();
            // Navigasyon alanı formdan gelmez; nullable açık olduğu için MVC
            // bunu zorunlu sayıyor ve doğrulama hiçbir zaman geçmiyordu.
            ModelState.Remove(nameof(Sponsor.Conference));

            if (!ModelState.IsValid) { SetViewBag(existing.Conference, slug); return View(model); }

            existing.Name = model.Name.Trim();
            existing.LogoUrl = model.LogoUrl?.Trim();
            existing.WebsiteUrl = model.WebsiteUrl?.Trim();
            existing.Tier = model.Tier;
            existing.SortOrder = model.SortOrder;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{existing.Name} güncellendi.";
            return Redirect($"/{slug}/Admin/Sponsors?conferenceId={existing.ConferenceId}");
        }

        // ── Delete ───────────────────────────────────────────────────────────

        [HttpPost("/{slug}/Admin/Sponsors/Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id)
        {
            var sponsor = await _context.Sponsors.FindAsync(id);
            if (sponsor == null) return NotFound();
            var confId = sponsor.ConferenceId;
            _context.Sponsors.Remove(sponsor);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Sponsor silindi.";
            return Redirect($"/{slug}/Admin/Sponsors?conferenceId={confId}");
        }
    }
}
