using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
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
    public class ReviewCriteriaController : Controller
    {
        // Kurucu metoda dokunmadan çeviri: mesajlar eskiden doğrudan Türkçe
        // yazılıydı, İngilizce seçili kullanıcıya da Türkçe dönüyorlardı.
        private string T(string key, string fallback)
        {
            var value = HttpContext?.RequestServices
                .GetService<IStringLocalizer<ReviewCriteriaController>>()?[key];

            return value == null || value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private readonly AppDbContext _context;
        private readonly IAdminTenantAccessService _tenantAccess;

        public ReviewCriteriaController(AppDbContext context, IAdminTenantAccessService tenantAccess)
        {
            _context = context;
            _tenantAccess = tenantAccess;
        }

        // ── Liste ────────────────────────────────────────────────────────────────

        [HttpGet("/Admin/ReviewCriteria")]
        [HttpGet("/{slug}/Admin/ReviewCriteria")]
        public async Task<IActionResult> Index(string? slug, Guid? conferenceId)
        {
            var conference = await GetConferenceAsync(slug, conferenceId);
            if (conference == null) return RedirectToAction("SelectConference", "Submissions");

            var criteria = await _context.ReviewCriteria
                .AsNoTracking()
                .Where(c => c.ConferenceId == conference.Id)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            ViewBag.Conference = conference;
            ViewBag.Slug = slug;
            return View(criteria);
        }

        // ── Oluştur ──────────────────────────────────────────────────────────────

        [HttpGet("/Admin/ReviewCriteria/Create")]
        [HttpGet("/{slug}/Admin/ReviewCriteria/Create")]
        public async Task<IActionResult> Create(string? slug, Guid? conferenceId)
        {
            var conference = await GetConferenceAsync(slug, conferenceId);
            if (conference == null) return RedirectToAction("SelectConference", "Submissions");

            ViewBag.Conference = conference;
            ViewBag.Slug = slug;
            return View(new ReviewCriterion { ConferenceId = conference.Id });
        }

        [HttpPost("/Admin/ReviewCriteria/Create")]
        [HttpPost("/{slug}/Admin/ReviewCriteria/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string? slug, ReviewCriterion model)
        {
            var conference = await GetConferenceAsync(slug, model.ConferenceId);
            if (conference == null) return RedirectToAction("SelectConference", "Submissions");

            if (!ModelState.IsValid)
            {
                ViewBag.Conference = conference;
                ViewBag.Slug = slug;
                return View(model);
            }

            model.Id = Guid.NewGuid();
            model.ConferenceId = conference.Id;
            model.CreatedAt = DateTime.UtcNow;
            _context.ReviewCriteria.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T("Msg_KriterOlusturuldu", "Kriter oluşturuldu.");
            return Redirect(IndexUrl(slug, conference.Id));
        }

        // ── Düzenle ──────────────────────────────────────────────────────────────

        [HttpGet("/Admin/ReviewCriteria/Edit/{id:guid}")]
        [HttpGet("/{slug}/Admin/ReviewCriteria/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string? slug, Guid id, Guid? conferenceId)
        {
            var criterion = await _context.ReviewCriteria.FindAsync(id);
            if (criterion == null) return NotFound();

            var conference = await GetConferenceAsync(slug, criterion.ConferenceId);
            if (conference == null) return Forbid();

            ViewBag.Conference = conference;
            ViewBag.Slug = slug;
            return View(criterion);
        }

        [HttpPost("/Admin/ReviewCriteria/Edit/{id:guid}")]
        [HttpPost("/{slug}/Admin/ReviewCriteria/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string? slug, Guid id, ReviewCriterion model)
        {
            var criterion = await _context.ReviewCriteria.FindAsync(id);
            if (criterion == null) return NotFound();

            var conference = await GetConferenceAsync(slug, criterion.ConferenceId);
            if (conference == null) return Forbid();

            if (!ModelState.IsValid)
            {
                ViewBag.Conference = conference;
                ViewBag.Slug = slug;
                return View(model);
            }

            criterion.Name = model.Name;
            criterion.NameEn = model.NameEn;
            criterion.Description = model.Description;
            criterion.Weight = model.Weight;
            criterion.SortOrder = model.SortOrder;
            criterion.IsActive = model.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T("Msg_KriterGuncellendi", "Kriter güncellendi.");
            return Redirect(IndexUrl(slug, conference.Id));
        }

        // ── Sil ──────────────────────────────────────────────────────────────────

        [HttpPost("/Admin/ReviewCriteria/Delete/{id:guid}")]
        [HttpPost("/{slug}/Admin/ReviewCriteria/Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string? slug, Guid id)
        {
            var criterion = await _context.ReviewCriteria
                .Include(c => c.Scores)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (criterion == null) return NotFound();

            var conference = await GetConferenceAsync(slug, criterion.ConferenceId);
            if (conference == null) return Forbid();

            if (criterion.Scores.Any())
            {
                TempData["ErrorMessage"] = T("Msg_BuKriterinPuanlariVarSilinemezPasif", "Bu kriterin puanları var, silinemez. Pasif hale getirin.");
                return Redirect(IndexUrl(slug, conference.Id));
            }

            _context.ReviewCriteria.Remove(criterion);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kriter silindi.";
            return Redirect(IndexUrl(slug, conference.Id));
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────────

        private async Task<Conference?> GetConferenceAsync(string? slug, Guid? conferenceId)
        {
            var query = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);
            if (conferenceId.HasValue && conferenceId != Guid.Empty)
                return await query.FirstOrDefaultAsync(c => c.Id == conferenceId);
            if (!string.IsNullOrWhiteSpace(slug))
                return await query.Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Tenant != null && c.Tenant.Slug == slug);
            return await query.OrderByDescending(c => c.StartDate).FirstOrDefaultAsync();
        }

        private static string IndexUrl(string? slug, Guid conferenceId)
            => string.IsNullOrWhiteSpace(slug)
                ? $"/Admin/ReviewCriteria?conferenceId={conferenceId}"
                : $"/{slug}/Admin/ReviewCriteria?conferenceId={conferenceId}";
    }
}
