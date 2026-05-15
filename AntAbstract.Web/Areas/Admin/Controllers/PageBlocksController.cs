using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.WebsiteBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
    [Authorize(Roles = "Admin")]
    public class PageBlocksController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<PageBlocksController> _localizer;

        public PageBlocksController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<PageBlocksController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            var user = await GetCurrentUserAsync();

            if (user == null || !user.TenantId.HasValue)
            {
                return null;
            }

            return user.TenantId.Value;
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string slug)
        {
            if (_tenantContext.Current == null)
            {
                return false;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return false;
            }

            return tenantId.Value == _tenantContext.Current.Id;
        }

        private async Task<Conference?> GetSelectedAccessibleConferenceAsync(string slug)
        {
            if (!await CanAccessCurrentTenantAsync(slug))
            {
                return null;
            }

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current!.Id);
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? _tenantContext.Current?.Slug ?? "";
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        private static List<(ConferencePageBlockType Type, string Title, int Order)> GetAcademicTemplate()
        {
            return new List<(ConferencePageBlockType Type, string Title, int Order)>
            {
                (ConferencePageBlockType.Hero, "Ana Karşılama (Hero)", 1),
                (ConferencePageBlockType.CallForPapers, "Kongreye Çağrı", 2),
                (ConferencePageBlockType.Topics, "Kongre Konuları", 3),
                (ConferencePageBlockType.Committees, "Bilim Kurulu", 4),
                (ConferencePageBlockType.About, "Hakem Değerlendirme Süreci", 5),
                (ConferencePageBlockType.Fees, "Katılım Ücreti", 6),
                (ConferencePageBlockType.About, "Kongre Programı", 7)
            };
        }

        [HttpGet("/{slug}/Admin/PageBlocks")]
        public async Task<IActionResult> Index(string slug)
        {
            var conference = await GetSelectedAccessibleConferenceAsync(slug);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen önce yetkili olduğunuz bir kongre seçiniz.");

                return Redirect($"/Admin/SelectConference?returnUrl=/{slug}/Admin/PageBlocks");
            }

            SetSelectedConferenceSession(conference);

            var blocks = await _context.ConferencePageBlocks
                .Where(b =>
                    b.ConferenceId == conference.Id &&
                    b.TenantId == conference.TenantId)
                .OrderBy(b => b.Order)
                .ToListAsync();

            var academicTemplate = GetAcademicTemplate();

            var isNewBlockAdded = false;

            foreach (var item in academicTemplate)
            {
                var exists = blocks.Any(b =>
                    b.ConferenceId == conference.Id &&
                    b.TenantId == conference.TenantId &&
                    b.Title == item.Title);

                if (!exists)
                {
                    var newBlock = new ConferencePageBlock
                    {
                        ConferenceId = conference.Id,
                        TenantId = conference.TenantId,
                        BlockType = item.Type,
                        Title = item.Title,
                        IsActive = true,
                        Order = item.Order,
                        CreatedAt = DateTime.UtcNow,
                        ContentJson = "{}"
                    };

                    _context.ConferencePageBlocks.Add(newBlock);
                    blocks.Add(newBlock);
                    isNewBlockAdded = true;
                }
            }

            if (isNewBlockAdded)
            {
                await _context.SaveChangesAsync();

                blocks = await _context.ConferencePageBlocks
                    .AsNoTracking()
                    .Where(b =>
                        b.ConferenceId == conference.Id &&
                        b.TenantId == conference.TenantId)
                    .OrderBy(b => b.Order)
                    .ToListAsync();
            }

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;

            return View(blocks);
        }

        [HttpGet("/{slug}/Admin/PageBlocks/Edit/{id:int}")]
        public async Task<IActionResult> Edit(string slug, int id)
        {
            if (!await CanAccessCurrentTenantAsync(slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu sayfa bloklarını düzenleme yetkiniz yok.");

                return Redirect($"/Admin/SelectConference?returnUrl=/{slug}/Admin/PageBlocks");
            }

            var block = await _context.ConferencePageBlocks
                .Include(b => b.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(b =>
                    b.Id == id &&
                    b.TenantId == _tenantContext.Current!.Id &&
                    b.Conference != null &&
                    b.Conference.TenantId == _tenantContext.Current.Id);

            if (block == null)
            {
                return NotFound(T(
                    "Error_BlockNotFoundOrUnauthorized",
                    "Blok bulunamadı veya bu bloğa erişim yetkiniz yok."));
            }

            ViewBag.BlockType = block.BlockType;
            ViewBag.Slug = slug;
            ViewBag.ConferenceId = block.ConferenceId;
            ViewBag.ConferenceTitle = block.Conference?.Title;

            if (block.BlockType == ConferencePageBlockType.About)
            {
                AboutBlockContent? content = null;

                if (!string.IsNullOrWhiteSpace(block.ContentJson))
                {
                    try
                    {
                        content = JsonSerializer.Deserialize<AboutBlockContent>(block.ContentJson);
                    }
                    catch
                    {
                        content = new AboutBlockContent();
                    }
                }

                ViewBag.AboutContent = content ?? new AboutBlockContent();
            }

            return View(block);
        }

        [HttpPost("/{slug}/Admin/PageBlocks/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string slug,
            int id,
            ConferencePageBlock model,
            AboutBlockContent aboutContent)
        {
            if (!await CanAccessCurrentTenantAsync(slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu sayfa bloklarını güncelleme yetkiniz yok.");

                return Redirect($"/Admin/SelectConference?returnUrl=/{slug}/Admin/PageBlocks");
            }

            var block = await _context.ConferencePageBlocks
                .Include(b => b.Conference)
                .FirstOrDefaultAsync(b =>
                    b.Id == id &&
                    b.TenantId == _tenantContext.Current!.Id &&
                    b.Conference != null &&
                    b.Conference.TenantId == _tenantContext.Current.Id);

            if (block == null)
            {
                return NotFound(T(
                    "Error_BlockNotFoundOrUnauthorized",
                    "Blok bulunamadı veya bu bloğa erişim yetkiniz yok."));
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

            TempData["SuccessMessage"] = T(
                "Success_BlockUpdated",
                "Blok başarıyla güncellendi.");

            return Redirect($"/{slug}/Admin/PageBlocks");
        }
    }
}