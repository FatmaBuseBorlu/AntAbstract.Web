using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.ConferenceTopics;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ConferenceTopicsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<ConferenceTopicsController> _localizer;

        public ConferenceTopicsController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<ConferenceTopicsController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _localizer = localizer;
        }

        private string L(string key, string fallback)
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

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            if (_tenantContext.Current == null)
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

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!tenantId.HasValue)
            {
                return query.Where(c => false);
            }

            return query.Where(c => c.TenantId == tenantId.Value);
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
            if (_tenantContext.Current == null)
            {
                return null;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!await CanAccessCurrentTenantAsync())
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
                    c.TenantId == _tenantContext.Current.Id);
        }

        [HttpGet("/Admin/ConferenceTopics")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                TempData["ErrorMessage"] = L(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedConferenceQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedConferenceQuery
                    .FirstOrDefaultAsync(c => c.Id == selectedId.Value);

                if (selectedConference?.Tenant?.Slug != null)
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect($"/{selectedConference.Tenant.Slug}/Admin/ConferenceTopics?conferenceId={selectedConference.Id}");
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var model = new SelectConferenceViewModel
            {
                Title = L("SelectConference_Title", "Kongre Seç"),
                Lead = L("SelectConference_Lead", "Bildiri konularını yönetmek için bir kongre seçiniz."),
                PostUrl = "/Admin/ConferenceTopics/Select",
                SubmitText = L("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", model);
        }

        [HttpPost("/Admin/ConferenceTopics/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = L(
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
                TempData["ErrorMessage"] = L(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceTopics?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/ConferenceTopics")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = L(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var usageDict = await _context.Submissions
                .AsNoTracking()
                .Where(s =>
                    s.ConferenceId == conference.Id &&
                    s.ConferenceTopicId.HasValue)
                .GroupBy(s => s.ConferenceTopicId!.Value)
                .Select(g => new
                {
                    Id = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            var items = await _context.ConferenceTopics
                .AsNoTracking()
                .Where(t => t.ConferenceId == conference.Id)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Name)
                .Select(t => new AdminConferenceTopicRowModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    NameEn = t.NameEn,
                    Description = t.Description,
                    DescriptionEn = t.DescriptionEn,
                    IsActive = t.IsActive,
                    SortOrder = t.SortOrder
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.SubmissionCount = usageDict.TryGetValue(item.Id, out var count)
                    ? count
                    : 0;
            }

            var model = new AdminConferenceTopicsIndexModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                Items = items
            };

            return View("~/Areas/Admin/Views/ConferenceTopics/Index.cshtml", model);
        }

        [HttpGet("/{slug}/Admin/ConferenceTopics/Create")]
        public async Task<IActionResult> Create(string slug, string? returnUrl = null)
        {
            var model = await BuildFormModel(slug, null, returnUrl);

            if (model == null)
            {
                TempData["ErrorMessage"] = L(
                    "Error_UnauthorizedTenant",
                    "Bu kongrenin bildiri konularını yönetme yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            return View("~/Areas/Admin/Views/ConferenceTopics/Form.cshtml", model);
        }

        [HttpGet("/{slug}/Admin/ConferenceTopics/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id, string? returnUrl = null)
        {
            var model = await BuildFormModel(slug, id, returnUrl);

            if (model == null)
            {
                return NotFound();
            }

            return View("~/Areas/Admin/Views/ConferenceTopics/Form.cshtml", model);
        }

        [HttpPost("/{slug}/Admin/ConferenceTopics/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(string slug, AdminConferenceTopicFormModel model)
        {
            var conference = await GetAccessibleConferenceAsync(slug, model.ConferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = L(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
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
                    L("Error_NameRequired", "Konu adı zorunludur."));
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Admin/Views/ConferenceTopics/Form.cshtml", model);
            }

            ConferenceTopic entity;

            if (model.Id.HasValue)
            {
                entity = (await _context.ConferenceTopics
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
                entity = new ConferenceTopic
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conference.Id,
                    CreatedDate = DateTime.UtcNow
                };

                await _context.ConferenceTopics.AddAsync(entity);
            }

            entity.Name = name;

            entity.NameEn = string.IsNullOrWhiteSpace(model.NameEn)
                ? null
                : model.NameEn.Trim();

            entity.Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim();

            entity.DescriptionEn = string.IsNullOrWhiteSpace(model.DescriptionEn)
                ? null
                : model.DescriptionEn.Trim();

            entity.IsActive = model.IsActive;
            entity.SortOrder = model.SortOrder;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = model.Id.HasValue
                ? L("Success_TopicUpdated", "Bildiri konusu başarıyla güncellendi.")
                : L("Success_TopicCreated", "Bildiri konusu başarıyla oluşturuldu.");

            var fallback = $"/{slug}/Admin/ConferenceTopics?conferenceId={conference.Id}";

            var go = !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? model.ReturnUrl
                : fallback;

            return Redirect(go);
        }

        [HttpPost("/{slug}/Admin/ConferenceTopics/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id, string? returnUrl = null)
        {
            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            var conference = await GetAccessibleConferenceAsync(slug, selectedConferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = L(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            SetSelectedConferenceSession(conference);

            var entity = await _context.ConferenceTopics
                .FirstOrDefaultAsync(t =>
                    t.Id == id &&
                    t.ConferenceId == conference.Id);

            if (entity == null)
            {
                return NotFound();
            }

            var usage = await _context.Submissions
                .CountAsync(s => s.ConferenceTopicId == entity.Id);

            var back = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : $"/{slug}/Admin/ConferenceTopics?conferenceId={conference.Id}";

            if (usage > 0)
            {
                TempData["ErrorMessage"] = L(
                    "Error_TopicHasSubmissions",
                    "Bu konuya bağlı bildiri olduğu için silinemez. Pasif hale getirebilirsiniz.");

                return Redirect(back);
            }

            _context.ConferenceTopics.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = L(
                "Success_TopicDeleted",
                "Bildiri konusu silindi.");

            return Redirect(back);
        }

        private async Task<AdminConferenceTopicFormModel?> BuildFormModel(
            string slug,
            Guid? id,
            string? returnUrl)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!await CanAccessCurrentTenantAsync())
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
                    : $"/{slug}/Admin/ConferenceTopics?conferenceId={conference.Id}";

            if (!id.HasValue)
            {
                return new AdminConferenceTopicFormModel
                {
                    Slug = slug,
                    ConferenceId = conference.Id,
                    ConferenceTitle = conference.Title ?? "",
                    IsActive = true,
                    SortOrder = 0,
                    ReturnUrl = effectiveReturnUrl
                };
            }

            var entity = await _context.ConferenceTopics
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.Id == id.Value &&
                    t.ConferenceId == conference.Id);

            if (entity == null)
            {
                return null;
            }

            return new AdminConferenceTopicFormModel
            {
                Id = entity.Id,
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                Name = entity.Name,
                NameEn = entity.NameEn,
                Description = entity.Description,
                DescriptionEn = entity.DescriptionEn,
                IsActive = entity.IsActive,
                SortOrder = entity.SortOrder,
                ReturnUrl = effectiveReturnUrl
            };
        }
    }
}