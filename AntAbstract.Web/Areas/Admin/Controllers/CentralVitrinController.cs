using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.WebsiteBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    [Route("Admin/CentralVitrin/{action=Index}/{id?}")]
    public class CentralVitrinController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IStringLocalizer<CentralVitrinController> _localizer;

        public CentralVitrinController(
            AppDbContext context,
            IStringLocalizer<CentralVitrinController> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, Guid? tenantId, string? status)
        {
            var now = DateTime.Now;

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            ViewBag.TotalConferenceCount = await _context.Conferences.CountAsync();

            ViewBag.ActiveConferenceCount = await _context.Conferences
                .CountAsync(c => c.StartDate <= now && c.EndDate >= now);

            ViewBag.UpcomingConferenceCount = await _context.Conferences
                .CountAsync(c => c.StartDate > now);

            ViewBag.CompletedConferenceCount = await _context.Conferences
                .CountAsync(c => c.EndDate < now);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();

                query = query.Where(c =>
                    c.Title.Contains(keyword) ||
                    (c.Slug != null && c.Slug.Contains(keyword)) ||
                    (c.City != null && c.City.Contains(keyword)) ||
                    (c.Country != null && c.Country.Contains(keyword)) ||
                    (c.Tenant != null && c.Tenant.Name.Contains(keyword)) ||
                    (c.Tenant != null && c.Tenant.Slug.Contains(keyword)));
            }

            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                query = query.Where(c => c.TenantId == tenantId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                status = status.Trim();

                if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.StartDate <= now && c.EndDate >= now);
                }
                else if (status.Equals("Upcoming", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.StartDate > now);
                }
                else if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.EndDate < now);
                }
            }

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.SelectedTenantId = tenantId;
            ViewBag.SelectedStatus = status;

            ViewBag.Tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name,
                    Selected = tenantId.HasValue && tenantId.Value == t.Id
                })
                .ToListAsync();

            return View(conferences);
        }

        [HttpGet]
        public async Task<IActionResult> ManageBlocks(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            ViewBag.ConferenceName = conference.Title;
            ViewBag.ConferenceId = conference.Id;

            var blocks = await _context.ConferencePageBlocks
                .Where(b => b.ConferenceId == conferenceId)
                .OrderBy(b => b.Order)
                .ToListAsync();

            var academicTemplate = new List<(ConferencePageBlockType Type, string Title, int Order)>
            {
                (ConferencePageBlockType.Hero, "Ana Karşılama (Hero)", 1),
                (ConferencePageBlockType.CallForPapers, "Kongreye Çağrı", 2),
                (ConferencePageBlockType.Topics, "Kongre Konuları", 3),
                (ConferencePageBlockType.Committees, "Bilim Kurulu", 4),
                (ConferencePageBlockType.About, "Hakem Değerlendirme Süreci", 5),
                (ConferencePageBlockType.Fees, "Katılım Ücreti", 6),
                (ConferencePageBlockType.About, "Kongre Programı", 7)
            };

            var isNewBlockAdded = false;

            foreach (var item in academicTemplate)
            {
                var exists = blocks.Any(b =>
                    b.Title == item.Title &&
                    b.ConferenceId == conferenceId);

                if (!exists)
                {
                    var newBlock = new ConferencePageBlock
                    {
                        ConferenceId = conferenceId,
                        TenantId = conference.TenantId,
                        BlockType = item.Type,
                        Title = item.Title,
                        IsActive = true,
                        Order = item.Order,
                        ContentJson = "{}",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ConferencePageBlocks.Add(newBlock);
                    blocks.Add(newBlock);

                    isNewBlockAdded = true;
                }
            }

            if (isNewBlockAdded)
            {
                await _context.SaveChangesAsync();

                blocks = blocks
                    .OrderBy(b => b.Order)
                    .ToList();
            }

            return View(blocks);
        }

        [HttpGet]
        public async Task<IActionResult> EditBlock(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var block = await _context.ConferencePageBlocks
                .Include(b => b.Conference)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (block == null)
            {
                return NotFound();
            }

            ViewBag.BlockType = block.BlockType;
            ViewBag.ConferenceId = block.ConferenceId;

            if (block.BlockType == ConferencePageBlockType.About)
            {
                var content = string.IsNullOrWhiteSpace(block.ContentJson)
                    ? new AboutBlockContent()
                    : JsonSerializer.Deserialize<AboutBlockContent>(block.ContentJson);

                ViewBag.AboutContent = content ?? new AboutBlockContent();
            }

            return View(block);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBlock(
            int id,
            ConferencePageBlock model,
            AboutBlockContent aboutContent)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id);

            if (block == null)
            {
                return NotFound();
            }

            block.Title = model.Title;
            block.Subtitle = model.Subtitle;
            block.IsActive = model.IsActive;
            block.UpdatedAt = DateTime.UtcNow;

            if (block.BlockType == ConferencePageBlockType.About)
            {
                block.ContentJson = JsonSerializer.Serialize(aboutContent);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["BlockUpdatedSuccessfully"];

            return RedirectToAction(
                nameof(ManageBlocks),
                new { conferenceId = block.ConferenceId });
        }

        [HttpGet]
        public async Task<IActionResult> CreateBlock(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var conferenceExists = await _context.Conferences
                .AsNoTracking()
                .AnyAsync(x => x.Id == conferenceId);

            if (!conferenceExists)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            ViewBag.ConferenceId = conferenceId;

            return View(new ConferencePageBlock
            {
                ConferenceId = conferenceId,
                IsActive = true,
                BlockType = ConferencePageBlockType.About,
                ContentJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlock(ConferencePageBlock model)
        {
            if (model.ConferenceId == Guid.Empty)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(x => x.Id == model.ConferenceId);

            if (conference == null)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var lastOrder = await _context.ConferencePageBlocks
                .Where(b => b.ConferenceId == model.ConferenceId)
                .MaxAsync(b => (int?)b.Order) ?? 0;

            model.TenantId = conference.TenantId;
            model.Order = lastOrder + 1;
            model.CreatedAt = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(model.ContentJson))
            {
                model.ContentJson = "{}";
            }

            _context.ConferencePageBlocks.Add(model);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["BlockCreatedSuccessfully"];

            return RedirectToAction(
                nameof(ManageBlocks),
                new { conferenceId = model.ConferenceId });
        }
    }
}