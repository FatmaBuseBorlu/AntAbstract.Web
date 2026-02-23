using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var conferences = await _context.Conferences
                .Where(x => x.TenantId == tenant.Id)
                .OrderByDescending(x => x.StartDate)
                .ToListAsync();

            var selectedConferenceId = conferenceId ?? conferences.FirstOrDefault()?.Id;

            ViewBag.CultureList = new SelectList(new[] { "tr-TR", "en-US" }, culture);
            ViewBag.PageList = new SelectList(new[] { "Home", "About", "Contact" }, page);
            ViewBag.ConferenceList = new SelectList(conferences, "Id", "Title", selectedConferenceId);

            ViewBag.Culture = culture;
            ViewBag.Page = page;
            ViewBag.ConferenceId = selectedConferenceId;

            if (selectedConferenceId == null)
                return View(new List<ConferencePageBlock>());

            var blocks = await _context.ConferencePageBlocks
                .Where(x => x.TenantId == tenant.Id
                         && x.ConferenceId == selectedConferenceId
                         && x.Page == page
                         && x.Culture == culture)
                .OrderBy(x => x.Order)
                .ToListAsync();

            return View(blocks);
        }

        [HttpGet]
        public async Task<IActionResult> Create(Guid conferenceId, string culture = "tr-TR", string page = "Home")
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            var exists = await _context.Conferences.AnyAsync(x => x.Id == conferenceId && x.TenantId == tenant.Id);
            if (!exists) return NotFound("Geçerli bir konferans bulunamadı.");

            var model = new ConferencePageBlock
            {
                TenantId = tenant.Id,
                ConferenceId = conferenceId,
                Culture = culture,
                Page = page,
                IsActive = true,
                Order = 0,
                BlockType = ConferencePageBlockType.Hero,
                ContentJson = "{}" 
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ConferencePageBlock model, string? btnText, string? btnUrl, string? bgImage, string? sideImage)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            ModelState.Remove("TenantId");
            ModelState.Remove("Conference");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            model.TenantId = tenant.Id;

            var ok = await _context.Conferences.AnyAsync(x => x.Id == model.ConferenceId && x.TenantId == tenant.Id);
            if (!ok) return NotFound("Konferans bulunamadı.");

            if (!ModelState.IsValid)
                return View(model);

            if (model.BlockType == ConferencePageBlockType.Hero)
            {
                var heroContent = new
                {
                    buttonText = btnText,
                    buttonUrl = btnUrl,
                    backgroundImageUrl = bgImage,
                    imageUrl = sideImage
                };
                model.ContentJson = JsonSerializer.Serialize(heroContent);
            }

            model.CreatedAt = DateTime.UtcNow;

            _context.ConferencePageBlocks.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Blok başarıyla eklendi.";

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

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id);

            if (block == null) return NotFound();

            return View(block);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ConferencePageBlock model, string? btnText, string? btnUrl, string? bgImage, string? sideImage)
        {
            var tenant = _tenantContext.Current;
            if (tenant == null) return BadRequest("Tenant bulunamadı.");

            ModelState.Remove("TenantId");
            ModelState.Remove("Conference");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == model.Id && x.TenantId == tenant.Id);

            if (block == null) return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            if (model.BlockType == ConferencePageBlockType.Hero)
            {
                var heroContent = new
                {
                    buttonText = btnText,
                    buttonUrl = btnUrl,
                    backgroundImageUrl = bgImage,
                    imageUrl = sideImage
                };
                block.ContentJson = JsonSerializer.Serialize(heroContent);
            }

            else
            {

                block.ContentJson = model.ContentJson;
            }

            block.Page = model.Page;
            block.Culture = model.Culture;
            block.BlockType = model.BlockType;
            block.Title = model.Title;
            block.Subtitle = model.Subtitle;
            block.Order = model.Order;
            block.IsActive = model.IsActive;
            block.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Blok başarıyla güncellendi.";

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

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id);

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

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id);

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