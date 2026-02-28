using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.WebsiteBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public PageBlocksController(AppDbContext context, TenantContext tenantContext, ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/{slug}/Admin/PageBlocks")]
        public async Task<IActionResult> Index(string slug)
        {
            var confId = _selectedConferenceService.GetSelectedConferenceId();
            if (confId == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce bir kongre seçin.";
                return Redirect($"/{slug}/Admin/Submissions");
            }

            var blocks = await _context.ConferencePageBlocks
                .Where(b => b.ConferenceId == confId && b.TenantId == _tenantContext.Current.Id)
                .OrderBy(b => b.Order)
                .ToListAsync();

           
            if (!blocks.Any())
            {
                var defaultBlocks = new List<ConferencePageBlock>
                {
                    new ConferencePageBlock { ConferenceId = confId.Value, TenantId = _tenantContext.Current.Id, BlockType = ConferencePageBlockType.Hero, Title = "Ana Karşılama (Hero)", IsActive = true, Order = 1 },
                    new ConferencePageBlock { ConferenceId = confId.Value, TenantId = _tenantContext.Current.Id, BlockType = ConferencePageBlockType.About, Title = "Hakkımızda", IsActive = true, Order = 2 },
                    new ConferencePageBlock { ConferenceId = confId.Value, TenantId = _tenantContext.Current.Id, BlockType = ConferencePageBlockType.Sponsors, Title = "Sponsorlar", IsActive = false, Order = 3 },
                    new ConferencePageBlock { ConferenceId = confId.Value, TenantId = _tenantContext.Current.Id, BlockType = ConferencePageBlockType.FAQ, Title = "Sıkça Sorulan Sorular", IsActive = false, Order = 4 }
                };

                _context.ConferencePageBlocks.AddRange(defaultBlocks);
                await _context.SaveChangesAsync();

                blocks = defaultBlocks.OrderBy(b => b.Order).ToList();
            }

            return View(blocks);
        }

        [HttpGet("/{slug}/Admin/PageBlocks/Edit/{id}")]
        public async Task<IActionResult> Edit(string slug, int id)
        {
            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == _tenantContext.Current.Id);

            if (block == null) return NotFound("Blok bulunamadı veya yetkiniz yok.");

            ViewBag.BlockType = block.BlockType;

            // JSON verisini formlar için C# nesnesine çeviriyoruz
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
            TempData["SuccessMessage"] = "Blok başarıyla güncellendi!";

            return Redirect($"/{slug}/Admin/PageBlocks");
        }
    }
}