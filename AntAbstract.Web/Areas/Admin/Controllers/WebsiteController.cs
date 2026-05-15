using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator,Editor")]
    public class WebsiteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<WebsiteController> _localizer;

        public WebsiteController(
            AppDbContext context,
            TenantContext tenantContext,
            UserManager<AppUser> userManager,
            IStringLocalizer<WebsiteController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
            _localizer = localizer;
        }

        private string? CurrentSlug => RouteData.Values["slug"]?.ToString();

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                return false;
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return false;
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                return true;
            }

            return user.TenantId.HasValue &&
                   user.TenantId.Value == tenant.Id;
        }

        private async Task<bool> ConferenceBelongsToCurrentTenantAsync(Guid conferenceId)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                return false;
            }

            return await _context.Conferences
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == conferenceId &&
                    x.TenantId == tenant.Id);
        }

        private IActionResult RedirectToSafePage()
        {
            var slug = CurrentSlug;

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return Redirect($"/{slug}/Admin/Conferences");
            }

            return Redirect("/Dashboard/MyConferences");
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string culture = "tr-TR",
            string page = "Home",
            Guid? conferenceId = null)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                return BadRequest(T("Error_TenantNotFound", "Tenant bulunamadı."));
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini yönetme yetkiniz yok.");

                return RedirectToSafePage();
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
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
            {
                return View(new List<ConferencePageBlock>());
            }

            var conferenceExists = await ConferenceBelongsToCurrentTenantAsync(selectedConferenceId.Value);

            if (!conferenceExists)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ValidConferenceNotFound",
                    "Geçerli kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToSafePage();
            }

            var blocks = await _context.ConferencePageBlocks
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenant.Id &&
                    x.ConferenceId == selectedConferenceId &&
                    x.Page == page &&
                    x.Culture == culture)
                .OrderBy(x => x.Order)
                .ToListAsync();

            return View(blocks);
        }

        [HttpGet]
        public async Task<IActionResult> Create(
            Guid conferenceId,
            string culture = "tr-TR",
            string page = "Home")
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                return BadRequest(T("Error_TenantNotFound", "Tenant bulunamadı."));
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini oluşturma yetkiniz yok.");

                return RedirectToSafePage();
            }

            var exists = await ConferenceBelongsToCurrentTenantAsync(conferenceId);

            if (!exists)
            {
                return NotFound(T(
                    "Error_ValidConferenceNotFound",
                    "Geçerli kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));
            }

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
        public async Task<IActionResult> Create(
            ConferencePageBlock model,
            string? btnText,
            string? btnUrl,
            string? bgImage,
            string? sideImage)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                return BadRequest(T("Error_TenantNotFound", "Tenant bulunamadı."));
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini oluşturma yetkiniz yok.");

                return RedirectToSafePage();
            }

            ModelState.Remove("TenantId");
            ModelState.Remove("Conference");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            model.TenantId = tenant.Id;

            var ok = await ConferenceBelongsToCurrentTenantAsync(model.ConferenceId);

            if (!ok)
            {
                return NotFound(T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

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

            TempData["SuccessMessage"] = T(
                "Success_BlockCreated",
                "Blok başarıyla oluşturuldu.");

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

            if (tenant == null)
            {
                return BadRequest(T("Error_TenantNotFound", "Tenant bulunamadı."));
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini düzenleme yetkiniz yok.");

                return RedirectToSafePage();
            }

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.TenantId == tenant.Id);

            if (block == null)
            {
                return NotFound(T(
                    "Error_BlockNotFound",
                    "Blok bulunamadı veya bu bloğa erişim yetkiniz yok."));
            }

            var conferenceExists = await ConferenceBelongsToCurrentTenantAsync(block.ConferenceId);

            if (!conferenceExists)
            {
                return NotFound(T(
                    "Error_ValidConferenceNotFound",
                    "Geçerli kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));
            }

            return View(block);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            ConferencePageBlock model,
            string? btnText,
            string? btnUrl,
            string? bgImage,
            string? sideImage)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                return BadRequest(T("Error_TenantNotFound", "Tenant bulunamadı."));
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini düzenleme yetkiniz yok.");

                return RedirectToSafePage();
            }

            ModelState.Remove("TenantId");
            ModelState.Remove("Conference");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x =>
                    x.Id == model.Id &&
                    x.TenantId == tenant.Id);

            if (block == null)
            {
                return NotFound(T(
                    "Error_BlockNotFound",
                    "Blok bulunamadı veya bu bloğa erişim yetkiniz yok."));
            }

            var conferenceExists = await ConferenceBelongsToCurrentTenantAsync(block.ConferenceId);

            if (!conferenceExists)
            {
                return NotFound(T(
                    "Error_ValidConferenceNotFound",
                    "Geçerli kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

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

            TempData["SuccessMessage"] = T(
                "Success_BlockUpdated",
                "Blok başarıyla güncellendi.");

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

            if (tenant == null)
            {
                return BadRequest(T("Error_TenantNotFound", "Tenant bulunamadı."));
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini değiştirme yetkiniz yok.");

                return RedirectToSafePage();
            }

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.TenantId == tenant.Id);

            if (block == null)
            {
                return NotFound(T(
                    "Error_BlockNotFound",
                    "Blok bulunamadı veya bu bloğa erişim yetkiniz yok."));
            }

            var conferenceExists = await ConferenceBelongsToCurrentTenantAsync(block.ConferenceId);

            if (!conferenceExists)
            {
                return NotFound(T(
                    "Error_ValidConferenceNotFound",
                    "Geçerli kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));
            }

            block.IsActive = !block.IsActive;
            block.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = block.IsActive
                ? T("Success_BlockActivated", "Blok aktif hale getirildi.")
                : T("Success_BlockDeactivated", "Blok pasif hale getirildi.");

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

            if (tenant == null)
            {
                return BadRequest(T("Error_TenantNotFound", "Tenant bulunamadı."));
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini silme yetkiniz yok.");

                return RedirectToSafePage();
            }

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.TenantId == tenant.Id);

            if (block == null)
            {
                return NotFound(T(
                    "Error_BlockNotFound",
                    "Blok bulunamadı veya bu bloğa erişim yetkiniz yok."));
            }

            var conferenceExists = await ConferenceBelongsToCurrentTenantAsync(block.ConferenceId);

            if (!conferenceExists)
            {
                return NotFound(T(
                    "Error_ValidConferenceNotFound",
                    "Geçerli kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));
            }

            _context.ConferencePageBlocks.Remove(block);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_BlockDeleted",
                "Blok başarıyla silindi.");

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