using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator,Editor")]
    [Route("Admin/[controller]/{action=Index}")]
    [Route("{slug}/Admin/[controller]/{action=Index}")]
    public class WebsiteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public WebsiteController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private string? CurrentSlug =>
            RouteData.Values["slug"]?.ToString() ?? _tenantContext.Current?.Slug;

        private object IndexRouteValues(Guid? conferenceId, string culture, string pageName)
            => new { slug = CurrentSlug, conferenceId, culture, pageName };

        public async Task<IActionResult> Index(
            string culture = "tr-TR",
            string pageName = "Home",
            Guid? conferenceId = null)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null)
                return BadRequest("Tenant bulunamadı. Slug çözümleme başarısız.");

            var tenantId = tenant.Id;

            var conferences = await _context.Conferences
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.StartDate)
                .ToListAsync();

            var selectedConference =
                (conferenceId.HasValue
                    ? conferences.FirstOrDefault(x => x.Id == conferenceId.Value)
                    : null)
                ?? conferences.FirstOrDefault();

            if (selectedConference == null)
            {
                TempData["ErrorMessage"] = "Bu tenant için konferans bulunamadı.";
                ViewBag.Culture = culture;
                ViewBag.PageName = pageName;
                ViewBag.ConferenceId = null;
                ViewBag.Conferences = conferences;
                return View(new List<ConferencePageBlock>());
            }

            ViewBag.Culture = culture;
            ViewBag.PageName = pageName;
            ViewBag.ConferenceId = selectedConference.Id;
            ViewBag.Conferences = conferences;

            var blocks = await _context.ConferencePageBlocks
                .Where(x => x.TenantId == tenantId
                            && x.ConferenceId == selectedConference.Id
                            && x.Page == pageName
                            && x.Culture == culture)
                .OrderBy(x => x.Order)
                .ToListAsync();

            return View(blocks);
        }
        [HttpGet]
        public IActionResult CreateWithConference(Guid conferenceId, string culture = "tr-TR", string pageName = "Home")
        {
            return RedirectToAction(nameof(Create), new
            {
                slug = CurrentSlug,
                conferenceId,
                culture,
                pageName
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(Guid conferenceId, string culture = "tr-TR", string pageName = "Home")
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var tenantId = tenant.Id;

            var exists = await _context.Conferences.AnyAsync(x => x.Id == conferenceId && x.TenantId == tenantId);
            if (!exists) return NotFound("Konferans bulunamadı / bu tenant'a ait değil.");

            var model = new ConferencePageBlock
            {
                TenantId = tenantId,
                ConferenceId = conferenceId,
                Culture = culture,
                Page = pageName,
                IsActive = true,
                Order = 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ConferencePageBlock model)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var tenantId = tenant.Id;
            model.TenantId = tenantId;

            var ok = await _context.Conferences.AnyAsync(x => x.Id == model.ConferenceId && x.TenantId == tenantId);
            if (!ok) return NotFound("Konferans bulunamadı / bu tenant'a ait değil.");

            if (!ModelState.IsValid) return View(model);

            model.CreatedAt = DateTime.UtcNow;

            _context.ConferencePageBlocks.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Blok eklendi.";
            return RedirectToAction(nameof(Index), IndexRouteValues(model.ConferenceId, model.Culture, model.Page));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var tenantId = tenant.Id;

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

            if (block == null) return NotFound();

            return View(block);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ConferencePageBlock model)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var tenantId = tenant.Id;

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == model.Id && x.TenantId == tenantId);

            if (block == null) return NotFound();
            if (!ModelState.IsValid) return View(model);

            block.Page = model.Page;
            block.Culture = model.Culture;
            block.BlockType = model.BlockType;
            block.Title = model.Title;
            block.Subtitle = model.Subtitle;
            block.ContentJson = model.ContentJson;
            block.Order = model.Order;
            block.IsActive = model.IsActive;
            block.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Blok güncellendi.";
            return RedirectToAction(nameof(Index), IndexRouteValues(block.ConferenceId, block.Culture, block.Page));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var tenantId = tenant.Id;

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

            if (block == null) return NotFound();

            block.IsActive = !block.IsActive;
            block.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = block.IsActive ? "Blok aktif edildi." : "Blok pasif edildi.";
            return RedirectToAction(nameof(Index), IndexRouteValues(block.ConferenceId, block.Culture, block.Page));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var tenantId = tenant.Id;

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

            if (block == null) return NotFound();

            _context.ConferencePageBlocks.Remove(block);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Blok silindi.";
            return RedirectToAction(nameof(Index), IndexRouteValues(block.ConferenceId, block.Culture, block.Page));
        }
    }
}
