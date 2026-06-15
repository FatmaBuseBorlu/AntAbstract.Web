using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels;
using AntAbstract.Web.Models.ViewModels.Admin.Registrations;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Security;
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
    [Authorize(Policy = AdminPolicies.TenantAdminOnly)]
    public class RegistrationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IAdminTenantAccessService _tenantAccess;

        public RegistrationsController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IAdminTenantAccessService tenantAccess)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _tenantAccess = tenantAccess;
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

        private Guid? GetSelectedConferenceIdFromSession(Guid? tenantId = null)
        {
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                var tenantSpecificValue = HttpContext.Session.GetString(
                    $"SelectedConferenceId:{tenantId.Value}");

                if (Guid.TryParse(tenantSpecificValue, out var tenantSpecificConferenceId) &&
                    tenantSpecificConferenceId != Guid.Empty)
                {
                    return tenantSpecificConferenceId;
                }
            }

            var globalValue = HttpContext.Session.GetString("SelectedConferenceId");

            if (Guid.TryParse(globalValue, out var globalConferenceId) &&
                globalConferenceId != Guid.Empty)
            {
                return globalConferenceId;
            }

            return null;
        }

        private Guid? GetSelectedConferenceId(Guid? tenantId = null)
        {
            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedConferenceId.HasValue && selectedConferenceId.Value != Guid.Empty)
            {
                return selectedConferenceId.Value;
            }

            return GetSelectedConferenceIdFromSession(tenantId);
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug
                       ?? conference.Slug
                       ?? _tenantContext.Current?.Slug
                       ?? "";

            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        private string BuildRegistrationsUrl(string slug, Guid conferenceId)
        {
            return $"/{slug}/Admin/Registrations?conferenceId={conferenceId}";
        }

        private string GetSafeBackUrl(
            string? returnUrl,
            string slug,
            Guid conferenceId)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return BuildRegistrationsUrl(slug, conferenceId);
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
                selectedConferenceId = GetSelectedConferenceId(_tenantContext.Current?.Id);
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

        [HttpGet("/Admin/Registrations")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                TempData["ErrorMessage"] = "Admin hesabınıza bağlı kurum bulunamadı.";

                return Redirect("/Dashboard/MyConferences");
            }

            var selectedId = GetSelectedConferenceId(tenantId.Value);

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedConferenceQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedConferenceQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConference?.Tenant != null &&
                    !string.IsNullOrWhiteSpace(selectedConference.Tenant.Slug))
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect(BuildRegistrationsUrl(
                        selectedConference.Tenant.Slug,
                        selectedConference.Id));
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
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
        public async Task<IActionResult> SelectConferencePost(
            Guid conferenceId,
            string? returnUrl = null)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.";

                return RedirectToAction(nameof(SelectConference));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null ||
                conference.Tenant == null ||
                string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.";

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect(BuildRegistrationsUrl(
                conference.Tenant.Slug,
                conference.Id));
        }

        [HttpGet("/{slug}/Admin/Registrations")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId = null,
            string? search = null,
            string? paid = null,
            Guid? registrationTypeId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı veya bu kongrenin kayıtlarını görüntüleme yetkiniz yok.";

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Registrations" });
            }

            SetSelectedConferenceSession(conference);

            var registrationTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(t => t.ConferenceId == conference.Id)
                .OrderBy(t => t.Name)
                .Select(t => new RegistrationTypeLookupItem
                {
                    Id = t.Id,
                    Name = t.Name
                })
                .ToListAsync();

            var query = _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == conference.Id)
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(paid))
            {
                if (paid == "Paid")
                {
                    query = query.Where(r => r.IsPaid);
                }
                else if (paid == "Unpaid")
                {
                    query = query.Where(r => !r.IsPaid);
                }
            }

            if (registrationTypeId.HasValue && registrationTypeId.Value != Guid.Empty)
            {
                query = query.Where(r => r.RegistrationTypeId == registrationTypeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();

                query = query.Where(r =>
                    (
                        r.AppUser != null &&
                        (
                            (((r.AppUser.FirstName ?? "") + " " + (r.AppUser.LastName ?? "")).ToLower().Contains(keyword)) ||
                            ((r.AppUser.Email ?? "").ToLower().Contains(keyword)) ||
                            ((r.AppUser.UserName ?? "").ToLower().Contains(keyword))
                        )
                    )
                    ||
                    (
                        r.RegistrationType != null &&
                        ((r.RegistrationType.Name ?? "").ToLower().Contains(keyword))
                    )
                );
            }

            var items = await query
                .OrderByDescending(r => r.RegistrationDate)
                .Select(r => new AdminRegistrationRowModel
                {
                    Id = r.Id,

                    UserFullName = r.AppUser == null
                        ? ""
                        : (
                            ((r.AppUser.FirstName ?? "") + " " + (r.AppUser.LastName ?? "")).Trim() != ""
                                ? ((r.AppUser.FirstName ?? "") + " " + (r.AppUser.LastName ?? "")).Trim()
                                : (r.AppUser.UserName ?? r.AppUser.Email ?? "")
                        ),

                    UserEmail = r.AppUser == null
                        ? ""
                        : (r.AppUser.Email ?? ""),

                    RegistrationTypeName = r.RegistrationType == null
                        ? ""
                        : (r.RegistrationType.Name ?? ""),

                    Amount = r.Amount,

                    Currency = r.RegistrationType != null &&
                               !string.IsNullOrWhiteSpace(r.RegistrationType.Currency)
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
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
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
        public async Task<IActionResult> MarkPaid(
            string slug,
            Guid id,
            Guid? conferenceId = null,
            string? returnUrl = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Bu kongrenin kayıtlarını güncelleme yetkiniz yok.";

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Registrations" });
            }

            SetSelectedConferenceSession(conference);

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.ConferenceId == conference.Id);

            if (registration == null)
            {
                TempData["ErrorMessage"] = "Kayıt bulunamadı.";

                return Redirect(BuildRegistrationsUrl(slug, conference.Id));
            }

            if (registration.IsPaid)
            {
                TempData["InfoMessage"] = "Bu kayıt zaten ödenmiş görünüyor.";

                return Redirect(GetSafeBackUrl(returnUrl, slug, conference.Id));
            }

            registration.IsPaid = true;
            registration.PaymentDate ??= DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödeme durumu ödendi olarak güncellendi.";

            return Redirect(GetSafeBackUrl(returnUrl, slug, conference.Id));
        }

        [HttpPost("/{slug}/Admin/Registrations/MarkUnpaid")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnpaid(
            string slug,
            Guid id,
            Guid? conferenceId = null,
            string? returnUrl = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Bu kongrenin kayıtlarını güncelleme yetkiniz yok.";

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Registrations" });
            }

            SetSelectedConferenceSession(conference);

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.ConferenceId == conference.Id);

            if (registration == null)
            {
                TempData["ErrorMessage"] = "Kayıt bulunamadı.";

                return Redirect(BuildRegistrationsUrl(slug, conference.Id));
            }

            if (!registration.IsPaid)
            {
                TempData["InfoMessage"] = "Bu kayıt zaten ödenmemiş görünüyor.";

                return Redirect(GetSafeBackUrl(returnUrl, slug, conference.Id));
            }

            registration.IsPaid = false;
            registration.PaymentDate = null;
            registration.PaymentTransactionId = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödeme durumu ödenmedi olarak güncellendi.";

            return Redirect(GetSafeBackUrl(returnUrl, slug, conference.Id));
        }
    }
}
