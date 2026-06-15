using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.RegistrationTypes;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdminOnly)]
    public class RegistrationTypesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IStringLocalizer<RegistrationTypesController> _localizer;

        public RegistrationTypesController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IAdminTenantAccessService tenantAccess,
            IStringLocalizer<RegistrationTypesController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _tenantAccess = tenantAccess;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string slug)
        {
            return await _tenantAccess.CanAccessCurrentTenantAsync(
                User,
                slug,
                allowSuperAdmin: false);
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var query = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            return query
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();
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

        private async Task<Conference?> GetAccessibleConferenceAsync(
            string slug,
            Guid? conferenceId)
        {
            if (!await CanAccessCurrentTenantAsync(slug))
            {
                return null;
            }

            Guid? selectedConferenceId = null;

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                selectedConferenceId = conferenceId.Value;
            }
            else
            {
                selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            }

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

        [HttpGet("/Admin/RegistrationTypes")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedConferenceQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedConferenceQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConference?.Tenant?.Slug != null)
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect($"/{selectedConference.Tenant.Slug}/Admin/RegistrationTypes?conferenceId={selectedConference.Id}");
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kayıt Türleri"),
                Lead = T("SelectConference_Lead", "Kayıt türlerini yönetmek için önce kongre seçin."),
                PostUrl = "/Admin/RegistrationTypes/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/RegistrationTypes/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(
            Guid conferenceId,
            string? returnUrl = null)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            var conferenceQuery = await GetAccessibleConferenceQueryAsync();

            var conference = await conferenceQuery
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null ||
                conference.Tenant == null ||
                string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect($"/{conference.Tenant.Slug}/Admin/RegistrationTypes?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/RegistrationTypes")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongrenin kayıt türlerini görüntüleme yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });
            }

            SetSelectedConferenceSession(conference);

            var usageDict = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == conference.Id)
                .GroupBy(r => r.RegistrationTypeId)
                .Select(g => new
                {
                    Id = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            var items = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(t => t.ConferenceId == conference.Id)
                .OrderBy(t => t.Name)
                .Select(t => new AdminRegistrationTypeRowModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    NameEn = t.NameEn,
                    Description = t.Description,
                    DescriptionEn = t.DescriptionEn,
                    Price = t.Price,
                    Currency = t.Currency,
                    RoleName = string.IsNullOrWhiteSpace(t.RoleName) ? "Author" : t.RoleName
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.UsageCount = usageDict.TryGetValue(item.Id, out var count)
                    ? count
                    : 0;
            }

            var model = new AdminRegistrationTypesIndexModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                Items = items
            };

            return View("~/Areas/Admin/Views/RegistrationTypes/Index.cshtml", model);
        }

        [HttpGet("/{slug}/Admin/RegistrationTypes/Create")]
        public async Task<IActionResult> Create(
            string slug,
            string? returnUrl = null)
        {
            var model = await BuildFormModel(slug, null, returnUrl);

            if (model == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongrenin kayıt türlerini yönetme yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });
            }

            return View("~/Areas/Admin/Views/RegistrationTypes/Form.cshtml", model);
        }

        [HttpGet("/{slug}/Admin/RegistrationTypes/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(
            string slug,
            Guid id,
            string? returnUrl = null)
        {
            var model = await BuildFormModel(slug, id, returnUrl);

            if (model == null)
            {
                return NotFound();
            }

            return View("~/Areas/Admin/Views/RegistrationTypes/Form.cshtml", model);
        }

        [HttpPost("/{slug}/Admin/RegistrationTypes/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            string slug,
            AdminRegistrationTypeFormModel model)
        {
            var conference = await GetAccessibleConferenceAsync(slug, model.ConferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });
            }

            SetSelectedConferenceSession(conference);

            model.Slug = slug;
            model.ConferenceId = conference.Id;
            model.ConferenceTitle = conference.Title ?? "";

            var name = (model.Name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    T("Error_NameRequired", "Kayıt türü adı zorunludur."));
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Admin/Views/RegistrationTypes/Form.cshtml", model);
            }

            RegistrationType entity;

            if (model.Id.HasValue)
            {
                entity = (await _context.RegistrationTypes
                    .FirstOrDefaultAsync(t =>
                        t.Id == model.Id.Value &&
                        t.ConferenceId == conference.Id))!;

                if (entity == null)
                {
                    return NotFound();
                }
            }
            else
            {
                entity = new RegistrationType
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conference.Id
                };

                await _context.RegistrationTypes.AddAsync(entity);
            }

            entity.Name = name;

            entity.NameEn = string.IsNullOrWhiteSpace(model.NameEn)
                ? null
                : model.NameEn.Trim();

            entity.Description = string.IsNullOrWhiteSpace(model.Description)
                ? ""
                : model.Description.Trim();

            entity.DescriptionEn = string.IsNullOrWhiteSpace(model.DescriptionEn)
                ? null
                : model.DescriptionEn.Trim();

            entity.Price = model.Price;

            entity.Currency = string.IsNullOrWhiteSpace(model.Currency)
                ? "TRY"
                : model.Currency.Trim();

            var allowedRoles = new[] { "Author", "Listener" };
            entity.RoleName = allowedRoles.Contains(model.RoleName, StringComparer.OrdinalIgnoreCase)
                ? model.RoleName
                : "Author";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_RegistrationTypeSaved",
                "Kayıt türü başarıyla kaydedildi.");

            var fallback = $"/{slug}/Admin/RegistrationTypes?conferenceId={conference.Id}";

            var go = !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? model.ReturnUrl
                : fallback;

            return Redirect(go);
        }

        [HttpPost("/{slug}/Admin/RegistrationTypes/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            string slug,
            Guid id,
            string? returnUrl = null)
        {
            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            var conference = await GetAccessibleConferenceAsync(slug, selectedConferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });
            }

            SetSelectedConferenceSession(conference);

            var entity = await _context.RegistrationTypes
                .FirstOrDefaultAsync(t =>
                    t.Id == id &&
                    t.ConferenceId == conference.Id);

            if (entity == null)
            {
                return NotFound();
            }

            var back = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : $"/{slug}/Admin/RegistrationTypes?conferenceId={conference.Id}";

            var usage = await _context.Registrations
                .CountAsync(r => r.RegistrationTypeId == entity.Id);

            if (usage > 0)
            {
                TempData["ErrorMessage"] = T(
                    "Error_RegistrationTypeHasDependencies",
                    "Bu kayıt türü kullanıldığı için silinemez.");

                return Redirect(back);
            }

            _context.RegistrationTypes.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_RegistrationTypeDeleted",
                "Kayıt türü başarıyla silindi.");

            return Redirect(back);
        }

        private async Task<AdminRegistrationTypeFormModel?> BuildFormModel(
            string slug,
            Guid? id,
            string? returnUrl)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!await CanAccessCurrentTenantAsync(slug))
            {
                return null;
            }

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                return null;
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return null;
            }

            var effectiveReturnUrl =
                !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : $"/{slug}/Admin/RegistrationTypes?conferenceId={conference.Id}";

            if (!id.HasValue)
            {
                return new AdminRegistrationTypeFormModel
                {
                    Slug = slug,
                    ConferenceId = conference.Id,
                    ConferenceTitle = conference.Title ?? "",
                    Currency = "TRY",
                    ReturnUrl = effectiveReturnUrl
                };
            }

            var entity = await _context.RegistrationTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.Id == id.Value &&
                    t.ConferenceId == conference.Id);

            if (entity == null)
            {
                return null;
            }

            return new AdminRegistrationTypeFormModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                Id = entity.Id,
                Name = entity.Name,
                NameEn = entity.NameEn,
                Description = entity.Description,
                DescriptionEn = entity.DescriptionEn,
                Price = entity.Price,
                Currency = entity.Currency,
                RoleName = string.IsNullOrWhiteSpace(entity.RoleName) ? "Author" : entity.RoleName,
                ReturnUrl = effectiveReturnUrl
            };
        }
    }
}
