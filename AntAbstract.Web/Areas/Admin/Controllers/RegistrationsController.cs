using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using AntAbstract.Web.Models.ViewModels.Admin.Registrations;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class RegistrationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public RegistrationsController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        private async Task<Conference?> GetConferenceOrNull(string? slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null) return null;
            if (string.IsNullOrWhiteSpace(slug)) return null;

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return null;

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();
            if (conferenceId == null) return null;

            return await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);
        }

        [HttpGet("/Admin/Registrations")]
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

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Kayıtlar ve Ödemeler",
                Lead = "Kayıtları ve ödemeleri yönetmek için önce kongre seçin.",
                PostUrl = "/Admin/Registrations/Select",
                SubmitText = "Devam Et",
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Registrations/Select")]
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

        [HttpGet("/{slug}/Admin/Registrations")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId = null,
            string? search = null,
            string? paid = null,
            Guid? registrationTypeId = null)
        {
            if (_tenantContext.Current == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                _selectedConferenceService.SetSelectedConferenceId(conferenceId.Value);

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            var conf = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conf == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            var registrationTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(t => t.ConferenceId == conf.Id)
                .OrderBy(t => t.Name)
                .Select(t => new RegistrationTypeLookupItem
                {
                    Id = t.Id,
                    Name = t.Name
                })
                .ToListAsync();

            var q = _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == conf.Id)
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(paid))
            {
                if (paid == "Paid") q = q.Where(r => r.IsPaid);
                else if (paid == "Unpaid") q = q.Where(r => !r.IsPaid);
            }

            if (registrationTypeId.HasValue && registrationTypeId.Value != Guid.Empty)
                q = q.Where(r => r.RegistrationTypeId == registrationTypeId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();

                q = q.Where(r =>
                    (r.AppUser != null && (
                        (((r.AppUser.FirstName ?? "") + " " + (r.AppUser.LastName ?? "")).ToLower().Contains(s)) ||
                        ((r.AppUser.Email ?? "").ToLower().Contains(s))
                    ))
                    || (r.RegistrationType != null && ((r.RegistrationType.Name ?? "").ToLower().Contains(s)))
                );
            }

            var items = await q
                .OrderByDescending(r => r.RegistrationDate)
                .Select(r => new AdminRegistrationRowModel
                {
                    Id = r.Id,
                    UserFullName = r.AppUser == null ? "" : ((r.AppUser.FirstName ?? "") + " " + (r.AppUser.LastName ?? "")).Trim(),
                    UserEmail = r.AppUser == null ? "" : (r.AppUser.Email ?? ""),
                    RegistrationTypeName = r.RegistrationType == null ? "" : (r.RegistrationType.Name ?? ""),
                    Amount = r.Amount,
                    Currency = (r.RegistrationType != null && !string.IsNullOrWhiteSpace(r.RegistrationType.Currency))
                        ? r.RegistrationType.Currency
                        : "TRY",
                    IsPaid = r.IsPaid,
                    RegistrationDate = r.RegistrationDate,
                    PaymentDate = r.PaymentDate
                })
                .ToListAsync();

            var model = new AdminRegistrationsIndexModel
            {
                Slug = slug,
                ConferenceId = conf.Id,
                ConferenceTitle = conf.Title ?? "",
                Search = search,
                Paid = paid,
                RegistrationTypeId = registrationTypeId,
                RegistrationTypes = registrationTypes,
                Items = items
            };

            return View("~/Areas/Admin/Views/Registrations/Index.cshtml", model);
        }

        [HttpPost("/{slug}/Admin/Registrations/MarkPaid")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(string slug, Guid id, string? returnUrl = null)
        {
            var conf = await GetConferenceOrNull(slug, null);
            if (conf == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            var reg = await _context.Registrations
                .FirstOrDefaultAsync(x => x.Id == id && x.ConferenceId == conf.Id);

            if (reg == null) return NotFound();

            reg.IsPaid = true;
            reg.PaymentDate = reg.PaymentDate ?? DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödeme durumu güncellendi.";

            var back = (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                ? returnUrl
                : $"/{slug}/Admin/Registrations?conferenceId={conf.Id}";

            return Redirect(back);
        }

        [HttpPost("/{slug}/Admin/Registrations/MarkUnpaid")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnpaid(string slug, Guid id, string? returnUrl = null)
        {
            var conf = await GetConferenceOrNull(slug, null);
            if (conf == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            var reg = await _context.Registrations
                .FirstOrDefaultAsync(x => x.Id == id && x.ConferenceId == conf.Id);

            if (reg == null) return NotFound();

            reg.IsPaid = false;
            reg.PaymentDate = null;
            reg.PaymentTransactionId = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödeme durumu güncellendi.";

            var back = (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                ? returnUrl
                : $"/{slug}/Admin/Registrations?conferenceId={conf.Id}";

            return Redirect(back);
        }
    }
}
