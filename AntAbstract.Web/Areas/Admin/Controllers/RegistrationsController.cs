using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels.Admin.Registrations;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                Lead = "Kayıt ve ödeme bilgilerini görmek için önce kongre seçin.",
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
                .AsNoTracking()
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

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            var regTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(rt => rt.ConferenceId == conference.Id)
                .OrderBy(rt => rt.Name)
                .Select(rt => new RegistrationTypeLookupItem
                {
                    Id = rt.Id,
                    Name = rt.Name
                })
                .ToListAsync();

            var query = _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Where(r => r.ConferenceId == conference.Id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(r =>
                    (r.AppUser != null && (
                        (r.AppUser.FirstName != null && r.AppUser.FirstName.Contains(s)) ||
                        (r.AppUser.LastName != null && r.AppUser.LastName.Contains(s)) ||
                        (r.AppUser.Email != null && r.AppUser.Email.Contains(s))
                    )) ||
                    (r.RegistrationType != null && r.RegistrationType.Name != null && r.RegistrationType.Name.Contains(s))
                );
            }

            if (!string.IsNullOrWhiteSpace(paid))
            {
                if (paid.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(r => r.IsPaid);

                if (paid.Equals("Unpaid", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(r => !r.IsPaid);
            }

            if (registrationTypeId.HasValue && registrationTypeId.Value != Guid.Empty)
            {
                query = query.Where(r => r.RegistrationTypeId == registrationTypeId.Value);
            }

            var items = await query
                .OrderByDescending(r => r.RegistrationDate)
                .Select(r => new AdminRegistrationRowModel
                {
                    Id = r.Id,
                    UserFullName = r.AppUser == null ? "" : ((r.AppUser.FirstName ?? "") + " " + (r.AppUser.LastName ?? "")).Trim(),
                    UserEmail = r.AppUser != null ? (r.AppUser.Email ?? "") : "",
                    RegistrationTypeName = r.RegistrationType != null ? (r.RegistrationType.Name ?? "") : "",
                    Amount = r.Amount,
                    Currency = r.RegistrationType != null ? (r.RegistrationType.Currency ?? "TRY") : "TRY",
                    IsPaid = r.IsPaid,
                    RegistrationDate = r.RegistrationDate,
                    PaymentDate = r.PaymentDate
                })
                .ToListAsync();

            var vm = new AdminRegistrationsIndexModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                Search = search,
                Paid = paid,
                Items = items,
                RegistrationTypeId = registrationTypeId,
                RegistrationTypes = regTypes
            };

            return View("~/Areas/Admin/Views/Registrations/Index.cshtml", vm);
        }

        [HttpGet("/{slug}/Admin/Registrations/Details/{id}")]
        public async Task<IActionResult> Details(string slug, Guid id, string? returnUrl = null)
        {
            if (_tenantContext.Current == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Registrations" });

            var reg = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(r => r.Id == id && r.Conference.TenantId == _tenantContext.Current.Id);

            if (reg == null)
                return NotFound();

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : $"/{slug}/Admin/Registrations";

            var vm = new AdminRegistrationDetailsModel
            {
                Id = reg.Id,
                Slug = slug,
                ConferenceId = reg.ConferenceId,
                ConferenceTitle = reg.Conference?.Title,

                UserFullName = reg.AppUser == null ? "" : ((reg.AppUser.FirstName ?? "") + " " + (reg.AppUser.LastName ?? "")).Trim(),
                UserEmail = reg.AppUser?.Email ?? "",

                RegistrationTypeName = reg.RegistrationType?.Name ?? "",
                RegistrationTypeDescription = reg.RegistrationType?.Description,
                Amount = reg.Amount,
                Currency = reg.RegistrationType?.Currency ?? "TRY",

                IsPaid = reg.IsPaid,
                RegistrationDate = reg.RegistrationDate,
                PaymentDate = reg.PaymentDate,
                PaymentTransactionId = reg.PaymentTransactionId,

                ReturnUrl = effectiveReturnUrl
            };

            return View("~/Areas/Admin/Views/Registrations/Details.cshtml", vm);
        }
    }
}
