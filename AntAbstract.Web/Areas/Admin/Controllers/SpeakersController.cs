using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class SpeakersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public SpeakersController(
            AppDbContext context,
            IAdminTenantAccessService tenantAccess,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantAccess = tenantAccess;
            _selectedConferenceService = selectedConferenceService;
        }

        private bool IsSuperAdmin() => _tenantAccess.IsSuperAdmin(User);

        private async Task<Conference?> GetConferenceAsync(string slug, Guid? conferenceId)
        {
            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                var byId = await _context.Conferences.Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Id == conferenceId.Value);
                if (byId != null) return byId;
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return await _context.Conferences.Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Slug == slug || (c.Tenant != null && c.Tenant.Slug == slug));
            }

            return null;
        }

        private void SetViewBag(Conference conference, string slug)
        {
            ViewBag.Slug = slug;
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
        }

        // ── Index ────────────────────────────────────────────────────────────

        [HttpGet("/{slug}/Admin/Speakers")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceAsync(slug, conferenceId);
            if (conference == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return Redirect($"/{slug}/Admin/Reports");
            }

            SetViewBag(conference, slug);

            var speakers = await _context.InvitedSpeakers
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.FullName)
                .ToListAsync();

            return View(speakers);
        }

        // ── Create ───────────────────────────────────────────────────────────

        [HttpGet("/{slug}/Admin/Speakers/Create")]
        public async Task<IActionResult> Create(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceAsync(slug, conferenceId);
            if (conference == null) return NotFound();
            SetViewBag(conference, slug);
            return View(new InvitedSpeaker { ConferenceId = conference.Id });
        }

        [HttpPost("/{slug}/Admin/Speakers/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, InvitedSpeaker model)
        {
            var conference = await GetConferenceAsync(slug, model.ConferenceId != Guid.Empty ? model.ConferenceId : null);
            if (conference == null) return NotFound();

            if (!ModelState.IsValid)
            {
                SetViewBag(conference, slug);
                return View(model);
            }

            model.Id = Guid.NewGuid();
            model.ConferenceId = conference.Id;
            model.CreatedAt = DateTime.UtcNow;
            model.PhotoUrl = model.PhotoUrl?.Trim();
            model.WebsiteUrl = model.WebsiteUrl?.Trim();

            _context.InvitedSpeakers.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{model.FullName} başarıyla eklendi.";
            return Redirect($"/{slug}/Admin/Speakers?conferenceId={conference.Id}");
        }

        // ── Edit ─────────────────────────────────────────────────────────────

        [HttpGet("/{slug}/Admin/Speakers/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id, Guid? conferenceId)
        {
            var speaker = await _context.InvitedSpeakers
                .Include(s => s.Conference).ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (speaker == null) return NotFound();

            SetViewBag(speaker.Conference, slug);
            return View(speaker);
        }

        [HttpPost("/{slug}/Admin/Speakers/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string slug, Guid id, InvitedSpeaker model)
        {
            var existing = await _context.InvitedSpeakers
                .Include(s => s.Conference).ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                SetViewBag(existing.Conference, slug);
                return View(model);
            }

            existing.FullName = model.FullName.Trim();
            existing.Title = model.Title?.Trim();
            existing.Institution = model.Institution?.Trim();
            existing.Country = model.Country?.Trim();
            existing.PhotoUrl = model.PhotoUrl?.Trim();
            existing.TalkTitle = model.TalkTitle?.Trim();
            existing.Bio = model.Bio?.Trim();
            existing.WebsiteUrl = model.WebsiteUrl?.Trim();
            existing.SortOrder = model.SortOrder;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{existing.FullName} güncellendi.";
            return Redirect($"/{slug}/Admin/Speakers?conferenceId={existing.ConferenceId}");
        }

        // ── Delete ───────────────────────────────────────────────────────────

        [HttpPost("/{slug}/Admin/Speakers/Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id)
        {
            var speaker = await _context.InvitedSpeakers.FindAsync(id);
            if (speaker == null) return NotFound();

            var confId = speaker.ConferenceId;
            _context.InvitedSpeakers.Remove(speaker);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Konuşmacı silindi.";
            return Redirect($"/{slug}/Admin/Speakers?conferenceId={confId}");
        }
    }
}
