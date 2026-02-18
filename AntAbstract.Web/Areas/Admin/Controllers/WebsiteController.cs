using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator,Editor")]
    public class WebsiteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public WebsiteController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private string? CurrentSlug => RouteData.Values["slug"]?.ToString();

        [HttpGet]
        public async Task<IActionResult> Index(string culture = "tr-TR", string page = "Home", Guid? conferenceId = null)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null)
                return BadRequest("Tenant bulunamadı. URL'yi /{slug}/Admin/Website şeklinde açmalısın.");

            var tenantId = tenant.Id;

            var conferences = await _context.Conferences
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.StartDate)
                .ToListAsync();

            var selectedConferenceId = conferenceId ?? conferences.FirstOrDefault()?.Id;

            ViewBag.Culture = culture;
            ViewBag.Page = page;
            ViewBag.ConferenceId = selectedConferenceId;

            ViewBag.CultureOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "tr-TR", Text = "tr-TR", Selected = culture == "tr-TR" },
                new SelectListItem { Value = "en-US", Text = "en-US", Selected = culture == "en-US" },
            };

            ViewBag.PageOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Home", Text = "Home", Selected = page == "Home" }
            };

            ViewBag.ConferenceOptions = conferences
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title,
                    Selected = selectedConferenceId.HasValue && c.Id == selectedConferenceId.Value
                })
                .ToList();

            var blocks = new List<ConferencePageBlock>();

            if (selectedConferenceId.HasValue)
            {
                blocks = await _context.ConferencePageBlocks
                    .Where(x => x.TenantId == tenantId
                                && x.ConferenceId == selectedConferenceId.Value
                                && x.Page == page
                                && x.Culture == culture)
                    .OrderBy(x => x.Order)
                    .ToListAsync();
            }

            return View(blocks);
        }

        [HttpGet]
        public async Task<IActionResult> Create(Guid conferenceId, string culture = "tr-TR", string page = "Home")
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var tenantId = tenant.Id;

            var exists = await _context.Conferences.AnyAsync(x => x.Id == conferenceId && x.TenantId == tenantId);
            if (!exists) return NotFound();

            var model = new ConferencePageBlock
            {
                TenantId = tenantId,
                ConferenceId = conferenceId,
                Culture = culture,
                Page = page,
                IsActive = true,
                Order = 0,
                BlockType = ConferencePageBlockType.Hero,
                ContentJson = "{\"buttonText\":\"Kayıt Ol\",\"buttonUrl\":\"/register\"}"
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
            if (!ok) return NotFound();

            if (!ModelState.IsValid) return View(model);

            model.CreatedAt = DateTime.UtcNow;
            _context.ConferencePageBlocks.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Blok eklendi.";

            return RedirectToAction(nameof(Index), new
            {
                slug = CurrentSlug,
                culture = model.Culture,
                page = model.Page,
                conferenceId = model.ConferenceId
            });
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

            return RedirectToAction(nameof(Index), new
            {
                slug = CurrentSlug,
                culture = block.Culture,
                page = block.Page,
                conferenceId = block.ConferenceId
            });
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

            return RedirectToAction(nameof(Index), new
            {
                slug = CurrentSlug,
                culture = block.Culture,
                page = block.Page,
                conferenceId = block.ConferenceId
            });
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

            return RedirectToAction(nameof(Index), new
            {
                slug = CurrentSlug,
                culture = block.Culture,
                page = block.Page,
                conferenceId = block.ConferenceId
            });
        }
    }
}
