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
    [Authorize(Roles = "Admin,SuperAdmin")]
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
                (ConferencePageBlockType.ImportantDates, "Önemli Tarihler", 4),
                (ConferencePageBlockType.Committees, "Bilim Kurulu", 5),
                (ConferencePageBlockType.Fees, "Katılım Ücreti", 6),
                (ConferencePageBlockType.Sponsors, "Sponsorlar", 7),
                (ConferencePageBlockType.FAQ, "Sık Sorulan Sorular", 8),
                (ConferencePageBlockType.Contact, "İletişim", 9),
                (ConferencePageBlockType.About, "Kongre Hakkında", 10)
            };
        }

        private async Task EnsureSiteSectionTemplatesSeededAsync()
        {
            var exists = await _context.SiteSectionTemplates.AnyAsync();

            if (exists)
            {
                return;
            }

            var templates = new List<SiteSectionTemplate>
            {
                new SiteSectionTemplate
                {
                    Order = 1,
                    BlockType = ConferencePageBlockType.Hero,
                    NameTr = "Ana Karşılama",
                    NameEn = "Hero Section",
                    Description = "Kongre sitesinin en üst tanıtım alanıdır.",
                    IsDefault = true,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 2,
                    BlockType = ConferencePageBlockType.CallForPapers,
                    NameTr = "Kongreye Çağrı",
                    NameEn = "Call for Papers",
                    Description = "Bildiri çağrısı ve başvuru yönlendirme alanıdır.",
                    IsDefault = true,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 3,
                    BlockType = ConferencePageBlockType.Topics,
                    NameTr = "Kongre Konuları",
                    NameEn = "Conference Topics",
                    Description = "Kongrede kabul edilen konu başlıklarını gösterir.",
                    IsDefault = true,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 4,
                    BlockType = ConferencePageBlockType.ImportantDates,
                    NameTr = "Önemli Tarihler",
                    NameEn = "Important Dates",
                    Description = "Bildiri gönderimi, kayıt ve kongre tarihleri gibi önemli tarihleri gösterir.",
                    IsDefault = true,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 5,
                    BlockType = ConferencePageBlockType.Committees,
                    NameTr = "Bilim Kurulu",
                    NameEn = "Scientific Committee",
                    Description = "Bilim kurulu ve düzenleme kurulu üyelerini gösterir.",
                    IsDefault = true,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 6,
                    BlockType = ConferencePageBlockType.Fees,
                    NameTr = "Katılım Ücreti",
                    NameEn = "Participation Fees",
                    Description = "Kayıt ve katılım ücretlerinin gösterildiği alandır.",
                    IsDefault = true,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 7,
                    BlockType = ConferencePageBlockType.Sponsors,
                    NameTr = "Sponsorlar",
                    NameEn = "Sponsors",
                    Description = "Sponsor logoları ve destekçi kurumların gösterildiği alandır.",
                    IsDefault = false,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 8,
                    BlockType = ConferencePageBlockType.FAQ,
                    NameTr = "Sık Sorulan Sorular",
                    NameEn = "Frequently Asked Questions",
                    Description = "Katılımcıların sık sorduğu soruların gösterildiği alandır.",
                    IsDefault = false,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 9,
                    BlockType = ConferencePageBlockType.Contact,
                    NameTr = "İletişim",
                    NameEn = "Contact",
                    Description = "Kongre iletişim bilgileri ve adres alanıdır.",
                    IsDefault = true,
                    IsActive = true
                },
                new SiteSectionTemplate
                {
                    Order = 10,
                    BlockType = ConferencePageBlockType.About,
                    NameTr = "Kongre Hakkında",
                    NameEn = "About the Conference",
                    Description = "Kongre hakkında detaylı açıklama metinleri için kullanılır.",
                    IsDefault = true,
                    IsActive = true
                }
            };

            _context.SiteSectionTemplates.AddRange(templates);
            await _context.SaveChangesAsync();
        }

        // =========================================================
        // SUPERADMIN - SITE BÖLÜM ŞABLONLARI
        // /Admin/PageBlocks
        // =========================================================

        [HttpGet("/Admin/PageBlocks")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SectionTemplates(
            string? search,
            string? status,
            string? defaultFilter)
        {
            await EnsureSiteSectionTemplatesSeededAsync();

            var query = _context.SiteSectionTemplates
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();

                query = query.Where(x =>
                    x.BlockType.ToString().Contains(keyword) ||
                    x.NameTr.Contains(keyword) ||
                    x.NameEn.Contains(keyword) ||
                    (x.Description != null && x.Description.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsActive);
                }
                else if (status.Equals("Passive", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => !x.IsActive);
                }
            }

            if (!string.IsNullOrWhiteSpace(defaultFilter) &&
                !defaultFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (defaultFilter.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.IsDefault);
                }
                else if (defaultFilter.Equals("Custom", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => !x.IsDefault);
                }
            }

            var model = await query
                .OrderBy(x => x.Order)
                .ThenBy(x => x.NameTr)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.DefaultFilter = defaultFilter;

            ViewBag.TotalCount = await _context.SiteSectionTemplates.CountAsync();
            ViewBag.ActiveCount = await _context.SiteSectionTemplates.CountAsync(x => x.IsActive);
            ViewBag.DefaultCount = await _context.SiteSectionTemplates.CountAsync(x => x.IsDefault);
            ViewBag.PassiveCount = await _context.SiteSectionTemplates.CountAsync(x => !x.IsActive);

            return View("SectionTemplates", model);
        }

        [HttpGet("/Admin/PageBlocks/Create")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> CreateTemplate()
        {
            var nextOrder = (await _context.SiteSectionTemplates
                .MaxAsync(x => (int?)x.Order)) ?? 0;

            return View("CreateTemplate", new SiteSectionTemplate
            {
                Order = nextOrder + 1,
                IsActive = true,
                IsDefault = false
            });
        }

        [HttpPost("/Admin/PageBlocks/Create")]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTemplate(SiteSectionTemplate model)
        {
            if (!Enum.IsDefined(typeof(ConferencePageBlockType), model.BlockType))
            {
                ModelState.AddModelError(nameof(model.BlockType), "Geçerli bir blok tipi seçiniz.");
            }

            if (!ModelState.IsValid)
            {
                return View("CreateTemplate", model);
            }

            var blockTypeExists = await _context.SiteSectionTemplates
                .AnyAsync(x => x.BlockType == model.BlockType);

            if (blockTypeExists)
            {
                ModelState.AddModelError(nameof(model.BlockType), "Bu blok tipi için zaten bir şablon tanımlı.");
                return View("CreateTemplate", model);
            }

            model.NameTr = model.NameTr.Trim();
            model.NameEn = model.NameEn.Trim();
            model.Description = model.Description?.Trim();
            model.CreatedAt = DateTime.UtcNow;

            _context.SiteSectionTemplates.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Site bölüm şablonu başarıyla oluşturuldu.";

            return Redirect("/Admin/PageBlocks");
        }

        [HttpGet("/Admin/PageBlocks/EditTemplate/{id:int}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> EditTemplate(int id)
        {
            var template = await _context.SiteSectionTemplates
                .FirstOrDefaultAsync(x => x.Id == id);

            if (template == null)
            {
                return NotFound();
            }

            return View("EditTemplate", template);
        }

        [HttpPost("/Admin/PageBlocks/EditTemplate/{id:int}")]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTemplate(int id, SiteSectionTemplate model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!Enum.IsDefined(typeof(ConferencePageBlockType), model.BlockType))
            {
                ModelState.AddModelError(nameof(model.BlockType), "Geçerli bir blok tipi seçiniz.");
            }

            if (!ModelState.IsValid)
            {
                return View("EditTemplate", model);
            }

            var template = await _context.SiteSectionTemplates
                .FirstOrDefaultAsync(x => x.Id == id);

            if (template == null)
            {
                return NotFound();
            }

            var blockTypeExists = await _context.SiteSectionTemplates
                .AnyAsync(x => x.Id != id && x.BlockType == model.BlockType);

            if (blockTypeExists)
            {
                ModelState.AddModelError(nameof(model.BlockType), "Bu blok tipi başka bir şablonda kullanılıyor.");
                return View("EditTemplate", model);
            }

            template.Order = model.Order;
            template.BlockType = model.BlockType;
            template.NameTr = model.NameTr.Trim();
            template.NameEn = model.NameEn.Trim();
            template.Description = model.Description?.Trim();
            template.IsDefault = model.IsDefault;
            template.IsActive = model.IsActive;
            template.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Site bölüm şablonu başarıyla güncellendi.";

            return Redirect("/Admin/PageBlocks");
        }

        [HttpPost("/Admin/PageBlocks/ToggleTemplate/{id:int}")]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTemplate(int id)
        {
            var template = await _context.SiteSectionTemplates
                .FirstOrDefaultAsync(x => x.Id == id);

            if (template == null)
            {
                return NotFound();
            }

            template.IsActive = !template.IsActive;
            template.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = template.IsActive
                ? "Şablon aktif hale getirildi."
                : "Şablon pasif hale getirildi.";

            return Redirect("/Admin/PageBlocks");
        }

        // =========================================================
        // KURUM ADMINI - SEÇİLİ KONGRENİN GERÇEK SITE BLOKLARI
        // /{slug}/Admin/PageBlocks
        // =========================================================

        [HttpGet("/{slug}/Admin/PageBlocks")]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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