using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.WebsiteBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    [Authorize(Roles = "Admin,Organizator")]
    public class PageBlocksController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IStringLocalizer<PageBlocksController> _localizer;

        public PageBlocksController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IStringLocalizer<PageBlocksController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _localizer = localizer;
        }

        [HttpGet("/{slug}/Admin/PageBlocks")]
        public async Task<IActionResult> Index(string slug)
        {
            var confId = _selectedConferenceService.GetSelectedConferenceId();
            if (confId == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_SelectConferenceFirst"];
                return Redirect($"/{slug}/Admin/Submissions");
            }

            var blocks = await _context.ConferencePageBlocks
                .Where(b => b.ConferenceId == confId && b.TenantId == _tenantContext.Current.Id)
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
                        ConferenceId = confId.Value,
                        TenantId = _tenantContext.Current.Id,
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

        [HttpGet("/{slug}/Admin/PageBlocks/Edit/{id}")]
        public async Task<IActionResult> Edit(string slug, int id)
        {
            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == _tenantContext.Current.Id);

            if (block == null)
                return NotFound(_localizer["Error_BlockNotFoundOrUnauthorized"]);

            ViewBag.BlockType = block.BlockType;

            if (block.BlockType == ConferencePageBlockType.About)
            {
                var content = string.IsNullOrEmpty(block.ContentJson)
                    ? new AboutBlockContent()
                    : JsonSerializer.Deserialize<AboutBlockContent>(block.ContentJson);
                ViewBag.AboutContent = content;
            }

            return View(block);
        }

        [HttpPost("/{slug}/Admin/PageBlocks/Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string slug, int id, ConferencePageBlock model, AboutBlockContent aboutContent)
        {
            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == _tenantContext.Current.Id);

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
            TempData["SuccessMessage"] = _localizer["Success_BlockUpdated"];

            return Redirect($"/{slug}/Admin/PageBlocks");
        }
    }
}
