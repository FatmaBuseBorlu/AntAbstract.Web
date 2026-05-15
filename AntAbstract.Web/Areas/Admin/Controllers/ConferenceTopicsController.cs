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
    [Authorize(Roles = "Admin,Organizator")]
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
            var text = _localizer[key].Value;

            return string.Equals(text, key, StringComparison.OrdinalIgnoreCase)
                ? fallback
                : text;
        }

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            if (_tenantContext.Current == null)
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
                   user.TenantId.Value == _tenantContext.Current.Id;
        }

        [HttpGet("/Admin/ConferenceTopics")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId != null)
            {
                var selectedConferenceQuery = _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .AsQueryable();

                if (!isAdmin)
                {
                    if (user?.TenantId == null)
                    {
                        selectedConferenceQuery = selectedConferenceQuery.Where(c => false);
                    }
                    else
                    {
                        selectedConferenceQuery = selectedConferenceQuery.Where(c => c.TenantId == user.TenantId.Value);
                    }
                }

                var selectedConference = await selectedConferenceQuery
                    .FirstOrDefaultAsync(c => c.Id == selectedId.Value);

                if (selectedConference?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", selectedConference.Tenant.Slug);
                    HttpContext.Session.SetString("SelectedConferenceTitle", selectedConference.Title ?? "");

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            slug = selectedConference.Tenant.Slug,
                            conferenceId = selectedConference.Id
                        });
                }
            }

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!isAdmin && user?.TenantId != null)
            {
                query = query.Where(c => c.TenantId == user.TenantId.Value);
            }
            else if (!isAdmin && user?.TenantId == null)
            {
                query = query.Where(c => false);
            }

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
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            var conferenceQuery = _context.Conferences
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!isAdmin)
            {
                if (user?.TenantId == null)
                {
                    TempData["ErrorMessage"] = L("Error_SelectConferenceUnauthorized", "Kongre seçme yetkiniz yok.");
                    return RedirectToAction(nameof(SelectConference));
                }

                conferenceQuery = conferenceQuery.Where(c => c.TenantId == user.TenantId.Value);
            }

            var conference = await conferenceQuery
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null ||
                conference.Tenant == null ||
                string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = L("Error_ConferenceNotFound", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");
                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", conference.Tenant.Slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    slug = conference.Tenant.Slug,
                    conferenceId = conference.Id
                });
        }

        [HttpGet("/{slug}/Admin/ConferenceTopics")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId = null)
        {
            if (_tenantContext.Current == null)
            {
                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = L("Error_UnauthorizedTenant", "Bu kongrenin bildiri konularını görüntüleme yetkiniz yok.");
                return RedirectToAction(nameof(SelectConference));
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

            if (selectedConferenceId == null || selectedConferenceId.Value == Guid.Empty)
            {
                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = L("Error_ConferenceNotFound", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

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
                TempData["ErrorMessage"] = L("Error_UnauthorizedTenant", "Bu kongrenin bildiri konularını yönetme yetkiniz yok.");

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
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = L("Error_UnauthorizedTenant", "Bu kongrenin bildiri konularını yönetme yetkiniz yok.");
                return RedirectToAction(nameof(SelectConference));
            }

            Guid? selectedConferenceId = null;

            if (model.ConferenceId != Guid.Empty)
            {
                selectedConferenceId = model.ConferenceId;
            }
            else
            {
                selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            }

            if (selectedConferenceId == null || selectedConferenceId.Value == Guid.Empty)
            {
                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = L("Error_ConferenceNotFound", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            model.Slug = slug;
            model.ConferenceId = conference.Id;
            model.ConferenceTitle = conference.Title ?? "";

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Admin/Views/ConferenceTopics/Form.cshtml", model);
            }

            ConferenceTopic entity;

            if (model.Id.HasValue)
            {
                entity = await _context.ConferenceTopics
                    .FirstOrDefaultAsync(t =>
                        t.Id == model.Id.Value &&
                        t.ConferenceId == conference.Id);

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

            var name = (model.Name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    L("Error_NameRequired", "Konu adı zorunludur."));

                return View("~/Areas/Admin/Views/ConferenceTopics/Form.cshtml", model);
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
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = L("Error_UnauthorizedTenant", "Bu kongrenin bildiri konularını silme yetkiniz yok.");
                return RedirectToAction(nameof(SelectConference));
            }

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedConferenceId == null || selectedConferenceId.Value == Guid.Empty)
            {
                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = L("Error_ConferenceNotFound", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/ConferenceTopics" });
            }

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

            TempData["SuccessMessage"] = L("Success_TopicDeleted", "Bildiri konusu silindi.");

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

            if (selectedConferenceId == null || selectedConferenceId.Value == Guid.Empty)
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