using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
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
    [Authorize(Policy = AdminPolicies.TenantAdminOnly)]
    public class WebsiteController : Controller
    {
        private const int MaxContentJsonLength = 10000;
        private const int MaxUrlLength = 500;

        private static readonly HashSet<string> SupportedCultures = new(
            new[] { "tr-TR", "en-US" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SupportedPages = new(
            new[] { "Home", "About", "Contact" },
            StringComparer.OrdinalIgnoreCase);

        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IStringLocalizer<WebsiteController> _localizer;

        public WebsiteController(
            AppDbContext context,
            TenantContext tenantContext,
            IAdminTenantAccessService tenantAccess,
            IStringLocalizer<WebsiteController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _tenantAccess = tenantAccess;
            _localizer = localizer;
        }

        private string CurrentSlug =>
            RouteData.Values["slug"]?.ToString()
            ?? _tenantContext.Current?.Slug
            ?? "";

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private static string NormalizeCulture(string? culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return "tr-TR";
            }

            return SupportedCultures.Contains(culture)
                ? culture
                : "tr-TR";
        }

        private static string NormalizePage(string? page)
        {
            if (string.IsNullOrWhiteSpace(page))
            {
                return "Home";
            }

            return SupportedPages.Contains(page)
                ? page
                : "Home";
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string? slug = null)
        {
            return await _tenantAccess.CanAccessCurrentTenantAsync(
                User,
                slug,
                allowSuperAdmin: false);
        }

        private async Task<bool> ConferenceBelongsToCurrentTenantAsync(Guid conferenceId)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null || conferenceId == Guid.Empty)
            {
                return false;
            }

            var adminTenantId = await GetCurrentAdminTenantIdAsync();

            if (!adminTenantId.HasValue || adminTenantId.Value != tenant.Id)
            {
                return false;
            }

            return await _context.Conferences
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == conferenceId &&
                    x.TenantId == tenant.Id);
        }

        private async Task<List<Conference>> GetCurrentTenantConferencesAsync()
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                return new List<Conference>();
            }

            var adminTenantId = await GetCurrentAdminTenantIdAsync();

            if (!adminTenantId.HasValue || adminTenantId.Value != tenant.Id)
            {
                return new List<Conference>();
            }

            return await _context.Conferences
                .AsNoTracking()
                .Where(x => x.TenantId == tenant.Id)
                .OrderByDescending(x => x.StartDate)
                .ToListAsync();
        }

        private async Task FillViewBagsAsync(
            string culture,
            string page,
            Guid? selectedConferenceId)
        {
            var conferences = await GetCurrentTenantConferencesAsync();

            ViewBag.CultureList = new SelectList(
                SupportedCultures.OrderByDescending(x => x),
                culture);

            ViewBag.PageList = new SelectList(
                SupportedPages,
                page);

            ViewBag.ConferenceList = new SelectList(
                conferences,
                "Id",
                "Title",
                selectedConferenceId);

            ViewBag.Culture = culture;
            ViewBag.Page = page;
            ViewBag.ConferenceId = selectedConferenceId;
            ViewBag.Slug = CurrentSlug;
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

        private IActionResult RedirectToWebsiteIndex(
            string culture,
            string page,
            Guid? conferenceId)
        {
            var slug = CurrentSlug;

            var query =
                $"culture={Uri.EscapeDataString(culture)}" +
                $"&page={Uri.EscapeDataString(page)}";

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                query += $"&conferenceId={conferenceId.Value}";
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return Redirect($"/{slug}/Admin/Website?{query}");
            }

            return Redirect($"/Admin/Website?{query}");
        }

        private bool IsSafeContentUrl(string? url, bool allowMailto = false)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return true;
            }

            url = url.Trim();

            if (url.Length > MaxUrlLength)
            {
                return false;
            }

            if (Url.IsLocalUrl(url))
            {
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps)
            {
                return true;
            }

            return allowMailto &&
                   string.Equals(uri.Scheme, "mailto", StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateHeroUrls(
            string? btnUrl,
            string? bgImage,
            string? sideImage)
        {
            if (!IsSafeContentUrl(btnUrl, allowMailto: true))
            {
                ModelState.AddModelError(
                    nameof(btnUrl),
                    T("Error_InvalidButtonUrl", "Buton bağlantısı geçersiz."));
            }

            if (!IsSafeContentUrl(bgImage))
            {
                ModelState.AddModelError(
                    nameof(bgImage),
                    T("Error_InvalidBackgroundImageUrl", "Arka plan görsel bağlantısı geçersiz."));
            }

            if (!IsSafeContentUrl(sideImage))
            {
                ModelState.AddModelError(
                    nameof(sideImage),
                    T("Error_InvalidSideImageUrl", "Yan görsel bağlantısı geçersiz."));
            }
        }

        private void ValidateContentJson(string? contentJson)
        {
            if (string.IsNullOrWhiteSpace(contentJson))
            {
                return;
            }

            if (contentJson.Length > MaxContentJsonLength)
            {
                ModelState.AddModelError(
                    nameof(ConferencePageBlock.ContentJson),
                    T("Error_ContentJsonTooLong", "İçerik JSON alanı çok uzun."));

                return;
            }

            try
            {
                using var _ = JsonDocument.Parse(contentJson);
            }
            catch
            {
                ModelState.AddModelError(
                    nameof(ConferencePageBlock.ContentJson),
                    T("Error_InvalidContentJson", "İçerik JSON formatı geçersiz."));
            }
        }

        private string BuildContentJson(
            ConferencePageBlockType blockType,
            string? rawContentJson,
            string? btnText,
            string? btnUrl,
            string? bgImage,
            string? sideImage)
        {
            if (blockType == ConferencePageBlockType.Hero)
            {
                var heroContent = new
                {
                    buttonText = btnText?.Trim(),
                    buttonUrl = btnUrl?.Trim(),
                    backgroundImageUrl = bgImage?.Trim(),
                    imageUrl = sideImage?.Trim()
                };

                return JsonSerializer.Serialize(heroContent);
            }

            return string.IsNullOrWhiteSpace(rawContentJson)
                ? "{}"
                : rawContentJson.Trim();
        }

        private void RemoveNavigationModelState()
        {
            ModelState.Remove("TenantId");
            ModelState.Remove("Tenant");
            ModelState.Remove("Conference");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");
        }

        [HttpGet("/Admin/Website")]
        [HttpGet("/{slug}/Admin/Website")]
        public async Task<IActionResult> Index(
            string? slug = null,
            string culture = "tr-TR",
            string page = "Home",
            Guid? conferenceId = null)
        {
            culture = NormalizeCulture(culture);
            page = NormalizePage(page);

            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantNotFound",
                    "Tenant bulunamadı.");

                return RedirectToSafePage();
            }

            if (!await CanAccessCurrentTenantAsync(slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini yönetme yetkiniz yok.");

                return RedirectToSafePage();
            }

            var conferences = await GetCurrentTenantConferencesAsync();

            var selectedConferenceId = conferenceId;

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                selectedConferenceId = conferences.FirstOrDefault()?.Id;
            }

            await FillViewBagsAsync(culture, page, selectedConferenceId);

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
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
                    x.ConferenceId == selectedConferenceId.Value &&
                    x.Page == page &&
                    x.Culture == culture)
                .OrderBy(x => x.Order)
                .ToListAsync();

            return View(blocks);
        }

        [HttpGet("/Admin/Website/Create")]
        [HttpGet("/{slug}/Admin/Website/Create")]
        public async Task<IActionResult> Create(
            string? slug = null,
            Guid conferenceId = default,
            string culture = "tr-TR",
            string page = "Home")
        {
            culture = NormalizeCulture(culture);
            page = NormalizePage(page);

            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantNotFound",
                    "Tenant bulunamadı.");

                return RedirectToSafePage();
            }

            if (!await CanAccessCurrentTenantAsync(slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini oluşturma yetkiniz yok.");

                return RedirectToSafePage();
            }

            var exists = await ConferenceBelongsToCurrentTenantAsync(conferenceId);

            if (!exists)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ValidConferenceNotFound",
                    "Geçerli kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToWebsiteIndex(culture, page, null);
            }

            await FillViewBagsAsync(culture, page, conferenceId);

            var model = new ConferencePageBlock
            {
                TenantId = tenant.Id,
                ConferenceId = conferenceId,
                Culture = culture,
                Page = page,
                IsActive = true,
                Order = 0,
                BlockType = ConferencePageBlockType.Hero,
                ContentJson = "{}",
                CreatedAt = DateTime.UtcNow
            };

            return View(model);
        }

        [HttpPost("/Admin/Website/Create")]
        [HttpPost("/{slug}/Admin/Website/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string? slug,
            ConferencePageBlock model,
            string? btnText,
            string? btnUrl,
            string? bgImage,
            string? sideImage)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantNotFound",
                    "Tenant bulunamadı.");

                return RedirectToSafePage();
            }

            if (!await CanAccessCurrentTenantAsync(slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini oluşturma yetkiniz yok.");

                return RedirectToSafePage();
            }

            RemoveNavigationModelState();

            model.TenantId = tenant.Id;
            model.Culture = NormalizeCulture(model.Culture);
            model.Page = NormalizePage(model.Page);

            var conferenceExists = await ConferenceBelongsToCurrentTenantAsync(model.ConferenceId);

            if (!conferenceExists)
            {
                ModelState.AddModelError(
                    nameof(model.ConferenceId),
                    T("Error_ConferenceNotFound", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));
            }

            if (model.BlockType == ConferencePageBlockType.Hero)
            {
                ValidateHeroUrls(btnUrl, bgImage, sideImage);
            }
            else
            {
                ValidateContentJson(model.ContentJson);
            }

            if (!ModelState.IsValid)
            {
                await FillViewBagsAsync(model.Culture, model.Page, model.ConferenceId);
                return View(model);
            }

            model.ContentJson = BuildContentJson(
                model.BlockType,
                model.ContentJson,
                btnText,
                btnUrl,
                bgImage,
                sideImage);

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = null;

            _context.ConferencePageBlocks.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_BlockCreated",
                "Blok başarıyla oluşturuldu.");

            return RedirectToWebsiteIndex(
                model.Culture,
                model.Page,
                model.ConferenceId);
        }

        [HttpGet("/Admin/Website/Edit/{id:int}")]
        [HttpGet("/{slug}/Admin/Website/Edit/{id:int}")]
        public async Task<IActionResult> Edit(
            string? slug,
            int id)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantNotFound",
                    "Tenant bulunamadı.");

                return RedirectToSafePage();
            }

            if (!await CanAccessCurrentTenantAsync(slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini düzenleme yetkiniz yok.");

                return RedirectToSafePage();
            }

            var block = await _context.ConferencePageBlocks
                .AsNoTracking()
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

            await FillViewBagsAsync(block.Culture, block.Page, block.ConferenceId);

            return View(block);
        }

        [HttpPost("/Admin/Website/Edit")]
        [HttpPost("/Admin/Website/Edit/{id:int}")]
        [HttpPost("/{slug}/Admin/Website/Edit")]
        [HttpPost("/{slug}/Admin/Website/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string? slug,
            int? id,
            ConferencePageBlock model,
            string? btnText,
            string? btnUrl,
            string? bgImage,
            string? sideImage)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantNotFound",
                    "Tenant bulunamadı.");

                return RedirectToSafePage();
            }

            if (!await CanAccessCurrentTenantAsync(slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu website içeriklerini düzenleme yetkiniz yok.");

                return RedirectToSafePage();
            }

            if (id.HasValue && id.Value != model.Id)
            {
                return NotFound();
            }

            RemoveNavigationModelState();

            model.Culture = NormalizeCulture(model.Culture);
            model.Page = NormalizePage(model.Page);

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

            if (model.BlockType == ConferencePageBlockType.Hero)
            {
                ValidateHeroUrls(btnUrl, bgImage, sideImage);
            }
            else
            {
                ValidateContentJson(model.ContentJson);
            }

            if (!ModelState.IsValid)
            {
                model.TenantId = block.TenantId;
                model.ConferenceId = block.ConferenceId;
                model.CreatedAt = block.CreatedAt;
                model.UpdatedAt = block.UpdatedAt;

                await FillViewBagsAsync(model.Culture, model.Page, block.ConferenceId);

                return View(model);
            }

            block.Page = model.Page;
            block.Culture = model.Culture;
            block.BlockType = model.BlockType;
            block.Title = model.Title;
            block.Subtitle = model.Subtitle;
            block.Order = model.Order;
            block.IsActive = model.IsActive;
            block.UpdatedAt = DateTime.UtcNow;

            block.ContentJson = BuildContentJson(
                model.BlockType,
                model.ContentJson,
                btnText,
                btnUrl,
                bgImage,
                sideImage);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_BlockUpdated",
                "Blok başarıyla güncellendi.");

            return RedirectToWebsiteIndex(
                block.Culture,
                block.Page,
                block.ConferenceId);
        }

        [HttpPost("/Admin/Website/Toggle")]
        [HttpPost("/{slug}/Admin/Website/Toggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(
            string? slug,
            int id)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantNotFound",
                    "Tenant bulunamadı.");

                return RedirectToSafePage();
            }

            if (!await CanAccessCurrentTenantAsync(slug))
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

            return RedirectToWebsiteIndex(
                block.Culture,
                block.Page,
                block.ConferenceId);
        }

        [HttpPost("/Admin/Website/Delete")]
        [HttpPost("/{slug}/Admin/Website/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            string? slug,
            int id)
        {
            var tenant = _tenantContext.Current;

            if (tenant == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantNotFound",
                    "Tenant bulunamadı.");

                return RedirectToSafePage();
            }

            if (!await CanAccessCurrentTenantAsync(slug))
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

            var culture = block.Culture;
            var page = block.Page;
            var conferenceId = block.ConferenceId;

            _context.ConferencePageBlocks.Remove(block);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_BlockDeleted",
                "Blok başarıyla silindi.");

            return RedirectToWebsiteIndex(
                culture,
                page,
                conferenceId);
        }
    }
}
