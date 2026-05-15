using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.WebsiteBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin")]
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

        public async Task<IActionResult> Index()
        {
            var allConferences = await _context.Conferences
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(allConferences);
        }

        public async Task<IActionResult> ManageBlocks(Guid conferenceId)
        {
            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
                return NotFound(_localizer["ConferenceNotFound"]);

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

            bool isNewBlockAdded = false;

            foreach (var item in academicTemplate)
            {
                if (!blocks.Any(b => b.Title == item.Title))
                {
                    var newBlock = new ConferencePageBlock
                    {
                        ConferenceId = conferenceId,
                        TenantId = conference.TenantId,
                        BlockType = item.Type,
                        Title = item.Title,
                        IsActive = true,
                        Order = item.Order,
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
                blocks = blocks.OrderBy(b => b.Order).ToList();
            }

            return View(blocks);
        }

        [HttpGet]
        public async Task<IActionResult> EditBlock(int id)
        {
            var block = await _context.ConferencePageBlocks
                .Include(b => b.Conference)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (block == null) return NotFound();

            ViewBag.BlockType = block.BlockType;
            ViewBag.ConferenceId = block.ConferenceId;

            if (block.BlockType == ConferencePageBlockType.About)
            {
                var content = string.IsNullOrEmpty(block.ContentJson)
                    ? new AboutBlockContent()
                    : JsonSerializer.Deserialize<AboutBlockContent>(block.ContentJson);
                ViewBag.AboutContent = content;
            }

            return View(block);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBlock(int id, ConferencePageBlock model, AboutBlockContent aboutContent)
        {
            var block = await _context.ConferencePageBlocks.FindAsync(id);
            if (block == null) return NotFound();

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

            return RedirectToAction(nameof(ManageBlocks), new { conferenceId = block.ConferenceId });
        }

        [HttpGet]
        public IActionResult CreateBlock(Guid conferenceId)
        {
            ViewBag.ConferenceId = conferenceId;
            return View(new ConferencePageBlock
            {
                ConferenceId = conferenceId,
                IsActive = true,
                BlockType = ConferencePageBlockType.About
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlock(ConferencePageBlock model)
        {
            var conference = await _context.Conferences.FindAsync(model.ConferenceId);
            if (conference == null) return NotFound();

            var lastOrder = await _context.ConferencePageBlocks
                .Where(b => b.ConferenceId == model.ConferenceId)
                .MaxAsync(b => (int?)b.Order) ?? 0;

            model.TenantId = conference.TenantId;
            model.Order = lastOrder + 1;
            model.CreatedAt = DateTime.UtcNow;

            _context.ConferencePageBlocks.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["BlockCreatedSuccessfully"];
            return RedirectToAction(nameof(ManageBlocks), new { conferenceId = model.ConferenceId });
        }
    }
}
