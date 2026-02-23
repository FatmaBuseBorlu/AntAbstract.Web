using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.RegistrationTypes;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class RegistrationTypesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;

        public RegistrationTypesController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
        }

        [HttpGet("/Admin/RegistrationTypes")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var conf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
                    HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return LocalRedirect(returnUrl);

                    return RedirectToAction(nameof(Index), new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

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

            var vm = new SelectConferenceViewModel
            {
                Title = "Kayıt Tipleri",
                Lead = "Kayıt tipi yönetimi için önce kongre seçin.",
                PostUrl = "/Admin/RegistrationTypes/Select",
                SubmitText = "Devam Et",
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/RegistrationTypes/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
        {
            var conf = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index), new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
        }

        [HttpGet("/{slug}/Admin/RegistrationTypes")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId = null)
        {
            if (_tenantContext.Current == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                _selectedConferenceService.SetSelectedConferenceId(conferenceId.Value);

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            var conf = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conf == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            var usageDict = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == conf.Id)
                .GroupBy(r => r.RegistrationTypeId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            var items = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(t => t.ConferenceId == conf.Id)
                .OrderBy(t => t.Name)
                .Select(t => new AdminRegistrationTypeRowModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    Price = t.Price,
                    Currency = t.Currency
                })
                .ToListAsync();

            foreach (var it in items)
                it.UsageCount = usageDict.TryGetValue(it.Id, out var c) ? c : 0;

            var model = new AdminRegistrationTypesIndexModel
            {
                Slug = slug,
                ConferenceId = conf.Id,
                ConferenceTitle = conf.Title ?? "",
                Items = items
            };

            return View("~/Areas/Admin/Views/RegistrationTypes/Index.cshtml", model);
        }

        [HttpGet("/{slug}/Admin/RegistrationTypes/Create")]
        public async Task<IActionResult> Create(string slug, string? returnUrl = null)
        {
            var model = await BuildFormModel(slug, null, returnUrl);
            if (model == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            return View("~/Areas/Admin/Views/RegistrationTypes/Form.cshtml", model);
        }

        [HttpGet("/{slug}/Admin/RegistrationTypes/Edit/{id}")]
        public async Task<IActionResult> Edit(string slug, Guid id, string? returnUrl = null)
        {
            var model = await BuildFormModel(slug, id, returnUrl);
            if (model == null) return NotFound();

            return View("~/Areas/Admin/Views/RegistrationTypes/Form.cshtml", model);
        }

        [HttpPost("/{slug}/Admin/RegistrationTypes/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(string slug, AdminRegistrationTypeFormModel model)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            var conf = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conf == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/RegistrationTypes/Form.cshtml", model);

            RegistrationType entity;

            if (model.Id.HasValue)
            {
                entity = await _context.RegistrationTypes
                    .FirstOrDefaultAsync(t => t.Id == model.Id.Value && t.ConferenceId == conf.Id);

                if (entity == null) return NotFound();
            }
            else
            {
                entity = new RegistrationType { ConferenceId = conf.Id };
                await _context.RegistrationTypes.AddAsync(entity);
            }

            var name = (model.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(nameof(model.Name), "Ad zorunludur.");
                return View("~/Areas/Admin/Views/RegistrationTypes/Form.cshtml", model);
            }

            entity.Name = name;

            entity.Description = string.IsNullOrWhiteSpace(model.Description)
                ? ""
                : model.Description.Trim();

            entity.Price = model.Price;

            entity.Currency = string.IsNullOrWhiteSpace(model.Currency)
                ? "TRY"
                : model.Currency.Trim();

            await _context.SaveChangesAsync();

            var fallback = $"/{slug}/Admin/RegistrationTypes";
            var go = (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                ? model.ReturnUrl
                : fallback;

            return Redirect(go);
        }

        [HttpPost("/{slug}/Admin/RegistrationTypes/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id, string? returnUrl = null)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            var conf = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conf == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/RegistrationTypes" });

            var entity = await _context.RegistrationTypes
                .FirstOrDefaultAsync(t => t.Id == id && t.ConferenceId == conf.Id);

            if (entity == null) return NotFound();

            var usage = await _context.Registrations.CountAsync(r => r.RegistrationTypeId == entity.Id);
            if (usage > 0)
            {
                TempData["ErrorMessage"] = "Bu kayıt tipine bağlı kayıtlar var. Önce kayıtları taşıyın ya da silin.";
                var back = (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : $"/{slug}/Admin/RegistrationTypes";
                return Redirect(back);
            }

            _context.RegistrationTypes.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kayıt tipi silindi.";
            return Redirect((!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : $"/{slug}/Admin/RegistrationTypes");
        }

        private async Task<AdminRegistrationTypeFormModel?> BuildFormModel(string slug, Guid? id, string? returnUrl)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return null;

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null) return null;

            var conf = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conf == null) return null;

            var effectiveReturnUrl = (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                ? returnUrl
                : $"/{slug}/Admin/RegistrationTypes";

            if (!id.HasValue)
            {
                return new AdminRegistrationTypeFormModel
                {
                    Slug = slug,
                    ConferenceId = conf.Id,
                    ConferenceTitle = conf.Title ?? "",
                    Currency = "TRY",
                    ReturnUrl = effectiveReturnUrl
                };
            }

            var entity = await _context.RegistrationTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id.Value && t.ConferenceId == conf.Id);

            if (entity == null) return null;

            return new AdminRegistrationTypeFormModel
            {
                Slug = slug,
                ConferenceId = conf.Id,
                ConferenceTitle = conf.Title ?? "",
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Price = entity.Price,
                Currency = entity.Currency,
                ReturnUrl = effectiveReturnUrl
            };
        }
    }
}