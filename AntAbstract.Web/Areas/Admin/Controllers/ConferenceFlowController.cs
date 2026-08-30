using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Assignment;
using AntAbstract.Web.Models.ViewModels.Admin.ConferenceFlow;
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

using Microsoft.Extensions.DependencyInjection;
namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class ConferenceFlowController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IStringLocalizer<ConferenceFlowController> _localizer;

        public ConferenceFlowController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IAdminTenantAccessService tenantAccess,
            IStringLocalizer<ConferenceFlowController> localizer)
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

        private bool IsSuperAdminUser()
        {
            return _tenantAccess.IsSuperAdmin(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            if (IsSuperAdminUser())
            {
                return true;
            }

            return await _tenantAccess.CanAccessCurrentTenantAsync(
                User,
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

        private async Task<Conference?> GetAccessibleConferenceAsync(string slug, Guid? conferenceId)
        {
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

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                return await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.Tenant != null &&
                    c.Tenant.Slug == slug);
            }

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

            return await query.FirstOrDefaultAsync(c =>
                c.Id == selectedConferenceId.Value &&
                c.TenantId == _tenantContext.Current.Id);
        }

        private void SetConferenceSession(Conference conference)
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

        private async Task<IActionResult> RedirectToSelectedConferenceOperationAsync(
            Guid? conferenceId,
            string operationAction)
        {
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
                TempData["ErrorMessage"] = T(
                    "Error_SelectedConferenceRequired",
                    "Lütfen önce bir kongre seçin.");

                return RedirectToAction(nameof(SelectConference));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);

            if (conference == null || conference.Tenant == null || string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectedConferenceNotFound",
                    T("Msg_KongreBulunamadiVeyaBuKongreyeErisim", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/{operationAction}?conferenceId={conference.Id}");
        }

        private async Task<IActionResult> RedirectToProgramSessionsPrintAsync(Guid? conferenceId)
        {
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
                TempData["ErrorMessage"] = T(
                    "Error_SelectedConferenceRequired",
                    "Lütfen önce bir kongre seçin.");

                return RedirectToAction(nameof(SelectConference));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);

            if (conference == null || conference.Tenant == null || string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectedConferenceNotFound",
                    T("Msg_KongreBulunamadiVeyaBuKongreyeErisim", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/ProgramSessions/Print?conferenceId={conference.Id}");
        }

        [HttpGet("/Admin/ConferenceFlow")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null, bool forceSelect = false)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!IsSuperAdminUser() && !tenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            if (!IsSuperAdminUser() && !forceSelect)
            {
                var selectedId = _selectedConferenceService.GetSelectedConferenceId();

                if (selectedId.HasValue && selectedId.Value != Guid.Empty)
                {
                    var selectedQuery = await GetAccessibleConferenceQueryAsync();

                    var selectedConf = await selectedQuery
                        .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                    if (selectedConf?.Tenant?.Slug != null)
                    {
                        SetConferenceSession(selectedConf);

                        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return LocalRedirect(returnUrl);
                        }

                        return Redirect($"/{selectedConf.Tenant.Slug}/Admin/ConferenceFlow?conferenceId={selectedConf.Id}");
                    }
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            if (!conferences.Any())
            {
                TempData["ErrorMessage"] = IsSuperAdminUser()
                    ? T("Msg_SistemdeGoruntulenebilecekKongreBulunamadi", "Sistemde görüntülenebilecek kongre bulunamadı.")
                    : T("Msg_KurumunuzaBagliGoruntulenebilecekKongreBulunamadi", "Kurumunuza bağlı görüntülenebilecek kongre bulunamadı.");
            }

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Akışı İçin Kongre Seç"),
                Lead = IsSuperAdminUser()
                    ? T("SelectConference_LeadSA", "SuperAdmin olarak sistemdeki tüm kongreleri görebilirsiniz. Akışını incelemek istediğiniz kongreyi seçiniz.")
                    : T("SelectConference_Lead", "Kongre akışını görüntülemek için kongre seçiniz."),
                PostUrl = "/Admin/ConferenceFlow/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/ConferenceFlow/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectedConferenceNotFound",
                    T("Msg_KongreBulunamadiVeyaBuKongreyeErisim", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));

                return RedirectToAction(nameof(SelectConference));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conf = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectedConferenceNotFound",
                    T("Msg_KongreBulunamadiVeyaBuKongreyeErisim", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok."));

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conf);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect($"/{conf.Tenant.Slug}/Admin/ConferenceFlow?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidTenant",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            var submissionCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s => s.ConferenceId == conference.Id);

            var assignedSubmissionCount = await (
                from ra in _context.ReviewAssignments.AsNoTracking()
                join s in _context.Submissions.AsNoTracking()
                    on ra.SubmissionId equals s.Id
                where s.ConferenceId == conference.Id
                select ra.SubmissionId
            ).Distinct().CountAsync();

            var decidedSubmissionCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s =>
                    s.ConferenceId == conference.Id &&
                    s.DecisionDate != null);

            var acceptedCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s =>
                    s.ConferenceId == conference.Id &&
                    s.Status == SubmissionStatus.Accepted);

            var rejectedCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s =>
                    s.ConferenceId == conference.Id &&
                    s.Status == SubmissionStatus.Rejected);

            var registrationCount = await _context.Registrations
                .AsNoTracking()
                .CountAsync(r => r.ConferenceId == conference.Id);

            var paidRegistrationCount = await _context.Registrations
                .AsNoTracking()
                .CountAsync(r =>
                    r.ConferenceId == conference.Id &&
                    r.IsPaid);

            var reviewerCount = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra =>
                    _context.Submissions
                        .Where(s => s.ConferenceId == conference.Id)
                        .Select(s => s.Id)
                        .Contains(ra.SubmissionId))
                .Select(ra => ra.ReviewerId)
                .Distinct()
                .CountAsync();

            var vm = new ConferenceFlowIndexViewModel
            {
                ConferenceId = conference.Id,
                TenantId = conference.TenantId,
                ConferenceTitle = conference.Title ?? "",
                Slug = slug,
                SubmissionCount = submissionCount,
                AssignedSubmissionCount = assignedSubmissionCount,
                DecidedSubmissionCount = decidedSubmissionCount,
                AcceptedCount = acceptedCount,
                RejectedCount = rejectedCount,
                RegistrationCount = registrationCount,
                PaidRegistrationCount = paidRegistrationCount,
                ReviewerCount = reviewerCount,
                IsSubmissionOpen = conference.IsSubmissionOpen,
                IsRegistrationOpen = conference.IsRegistrationOpen,
                IsBiddingOpen = conference.IsBiddingOpen,
                IsFullTextOpen = conference.IsFullTextOpen,
                ConferenceStartDate = conference.StartDate,
                ConferenceEndDate = conference.EndDate,
                AbstractSubmissionDeadline = conference.AbstractSubmissionDeadline
            };

            return View("~/Areas/Admin/Views/ConferenceFlow/Index.cshtml", vm);
        }

        [HttpGet("/Admin/ConferenceFlow/RegistrationsAndPayments")]
        public async Task<IActionResult> RegistrationsAndPaymentsRoot(Guid? conferenceId)
        {
            return await RedirectToSelectedConferenceOperationAsync(
                conferenceId,
                nameof(RegistrationsAndPayments));
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow/RegistrationsAndPayments")]
        public async Task<IActionResult> RegistrationsAndPayments(
            string slug,
            Guid? conferenceId,
            string? search = null,
            string? paymentStatus = null,
            Guid? registrationTypeId = null,
            int page = 1)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidTenant",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            var baseQuery = _context.Registrations
                .AsNoTracking()
                .Include(x => x.AppUser)
                .Include(x => x.RegistrationType)
                .Include(x => x.Conference)
                    .ThenInclude(x => x.Tenant)
                .Where(x => x.ConferenceId == conference.Id);

            var totalRegistrations = await baseQuery.CountAsync();
            var paidRegistrations = await baseQuery.CountAsync(x => x.IsPaid);
            var pendingPayments = await baseQuery.CountAsync(x => !x.IsPaid);

            // SQLite decimal uzerinde SUM yapamiyor (SQL Server yapabiliyor).
            // Tutarlar tek kolon olarak cekilip bellekte toplaniyor.
            var totalRevenue = (await baseQuery
                .Where(x => x.IsPaid)
                .Select(x => x.Amount)
                .ToListAsync())
                .Sum();

            // SQLite ORDER BY icinde decimal cevirmiyor (SQL Server cevirebiliyor).
            // Kayit turu sayisi az oldugu icin siralama bellekte yapiliyor.
            var registrationTypes = (await _context.RegistrationTypes
                .AsNoTracking()
                .Where(x => x.ConferenceId == conference.Id)
                .ToListAsync())
                .OrderBy(x => x.Price)
                .ThenBy(x => x.Name)
                .Select(x => new RegistrationTypeFilterItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    Price = x.Price,
                    Currency = x.Currency
                })
                .ToList();

            var filteredQuery = baseQuery;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchText = search.Trim();

                filteredQuery = filteredQuery.Where(x =>
                    (x.AppUser != null && (
                        (x.AppUser.FirstName != null && x.AppUser.FirstName.Contains(searchText)) ||
                        (x.AppUser.LastName != null && x.AppUser.LastName.Contains(searchText)) ||
                        (x.AppUser.Email != null && x.AppUser.Email.Contains(searchText))
                    )) ||
                    (x.RegistrationType != null && x.RegistrationType.Name.Contains(searchText)) ||
                    (x.BillingName != null && x.BillingName.Contains(searchText)) ||
                    (x.PaymentTransactionId != null && x.PaymentTransactionId.Contains(searchText))
                );
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                var normalizedStatus = paymentStatus.Trim().ToLowerInvariant();

                if (normalizedStatus == "paid")
                {
                    filteredQuery = filteredQuery.Where(x => x.IsPaid);
                }
                else if (normalizedStatus == "pending")
                {
                    filteredQuery = filteredQuery.Where(x => !x.IsPaid);
                }
                else if (normalizedStatus == "failed" || normalizedStatus == "cancelled")
                {
                    filteredQuery = filteredQuery.Where(x => false);
                }
            }

            if (registrationTypeId.HasValue && registrationTypeId.Value != Guid.Empty)
            {
                filteredQuery = filteredQuery.Where(x => x.RegistrationTypeId == registrationTypeId.Value);
            }

            const int pageSize = 30;
            if (page < 1) page = 1;

            var orderedQuery = filteredQuery.OrderByDescending(x => x.RegistrationDate);
            var filteredCount = await orderedQuery.CountAsync();

            var registrations = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = registrations
                .Select(x =>
                {
                    var fullName = x.AppUser == null
                        ? ""
                        : $"{x.AppUser.FirstName} {x.AppUser.LastName}".Trim();

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        fullName = x.AppUser?.UserName ?? x.AppUser?.Email ?? "Kullanıcı";
                    }

                    return new RegistrationPaymentRowItem
                    {
                        RegistrationId = x.Id,
                        UserId = x.AppUserId,
                        FullName = fullName,
                        Email = x.AppUser?.Email ?? "",
                        RegistrationTypeName = x.RegistrationType?.Name ?? "Kayıt türü yok",
                        Amount = x.Amount,
                        Currency = x.RegistrationType?.Currency ?? "TRY",
                        IsPaid = x.IsPaid,
                        RegistrationDate = x.RegistrationDate,
                        PaymentDate = x.PaymentDate,
                        PaymentTransactionId = x.PaymentTransactionId,
                        BillingName = x.BillingName,
                        TaxOffice = x.TaxOffice,
                        TaxNumber = x.TaxNumber
                    };
                })
                .ToList();

            var model = new RegistrationsAndPaymentsViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                Slug = slug,

                TotalRegistrations = totalRegistrations,
                PaidRegistrations = paidRegistrations,
                PendingPayments = pendingPayments,
                TotalRevenue = totalRevenue,

                Search = search ?? "",
                PaymentStatus = paymentStatus ?? "",
                RegistrationTypeId = registrationTypeId,

                RegistrationTypes = registrationTypes,
                Items = items,
                Page = page,
                PageSize = pageSize,
                FilteredCount = filteredCount
            };

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title ?? "";
            ViewBag.Slug = slug;

            return View("~/Areas/Admin/Views/ConferenceFlow/RegistrationsAndPayments.cshtml", model);
        }

        [HttpPost("/Admin/ConferenceFlow/RegistrationsAndPayments/ApprovePayment")]
        [HttpPost("/{slug}/Admin/ConferenceFlow/RegistrationsAndPayments/ApprovePayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRegistrationPayment(
            string? slug,
            Guid registrationId,
            Guid conferenceId,
            string? returnUrl = null)
        {
            if (registrationId == Guid.Empty || conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = T("Msg_GecersizKayitVeyaKongreBilgisi", "Geçersiz kayıt veya kongre bilgisi.");

                return RedirectToAction(nameof(SelectConference));
            }

            var conferenceQuery = await GetAccessibleConferenceQueryAsync();

            var conference = await conferenceQuery
                .FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conference == null || conference.Tenant == null || string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T("Msg_KongreBulunamadiVeyaBuKongreyeErisim", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            var registration = await _context.Registrations
                .Include(x => x.RegistrationType)
                .FirstOrDefaultAsync(x =>
                    x.Id == registrationId &&
                    x.ConferenceId == conference.Id);

            if (registration == null)
            {
                TempData["ErrorMessage"] = T("Msg_OnaylanacakKayitBulunamadi", "Onaylanacak kayıt bulunamadı.");

                return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/RegistrationsAndPayments?conferenceId={conference.Id}");
            }

            if (registration.IsPaid)
            {
                TempData["InfoMessage"] = T("Msg_BuKayitZatenOdenmisOlarakGorunuyor", "Bu kayıt zaten ödenmiş olarak görünüyor.");

                return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/RegistrationsAndPayments?conferenceId={conference.Id}");
            }

            var now = DateTime.UtcNow;

            if (registration.Amount <= 0 && registration.RegistrationType != null)
            {
                registration.Amount = registration.RegistrationType.Price;
            }

            var shortRegistrationId = registration.Id.ToString("N").Substring(0, 8);

            var transactionId = !string.IsNullOrWhiteSpace(registration.PaymentTransactionId)
                ? registration.PaymentTransactionId
                : $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}-{shortRegistrationId}";

            registration.IsPaid = true;
            registration.PaymentDate = now;
            registration.PaymentTransactionId = transactionId;

            var paymentExists = await _context.Payments
                .AnyAsync(x =>
                    x.ConferenceId == conference.Id &&
                    x.AppUserId == registration.AppUserId &&
                    x.TransactionId == transactionId &&
                    x.Status == PaymentStatus.Completed);

            if (!paymentExists)
            {
                _context.Payments.Add(new Payment
                {
                    Amount = registration.Amount,
                    Currency = registration.RegistrationType?.Currency ?? "TRY",
                    PaymentMethod = "Manual",
                    TransactionId = transactionId,
                    PaymentDate = now,
                    Status = PaymentStatus.Completed,
                    BillingName = registration.BillingName,
                    BillingAddress = registration.BillingAddress,
                    TaxOffice = registration.TaxOffice,
                    TaxNumber = registration.TaxNumber,
                    AppUserId = registration.AppUserId,
                    ConferenceId = conference.Id
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T("Msg_OdemeBasariylaOnaylandi", "Ödeme başarıyla onaylandı.");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/RegistrationsAndPayments?conferenceId={conference.Id}");
        }

        [HttpPost("/Admin/ConferenceFlow/RegistrationsAndPayments/RevokePayment")]
        [HttpPost("/{slug}/Admin/ConferenceFlow/RegistrationsAndPayments/RevokePayment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeRegistrationPayment(
            string? slug,
            Guid registrationId,
            Guid conferenceId,
            string? returnUrl = null)
        {
            if (registrationId == Guid.Empty || conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = T("Msg_GecersizKayitVeyaKongreBilgisi", "Geçersiz kayıt veya kongre bilgisi.");
                return RedirectToAction(nameof(SelectConference));
            }

            var conferenceQuery = await GetAccessibleConferenceQueryAsync();
            var conference = await conferenceQuery.FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conference == null || conference.Tenant == null || string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T("Msg_KongreBulunamadiVeyaBuKongreyeErisim", "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");
                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(x => x.Id == registrationId && x.ConferenceId == conference.Id);

            if (registration == null)
            {
                TempData["ErrorMessage"] = T("Msg_KayitBulunamadi", "Kayıt bulunamadı.");
                return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/RegistrationsAndPayments?conferenceId={conference.Id}");
            }

            if (!registration.IsPaid)
            {
                TempData["InfoMessage"] = T("Msg_BuKayitZatenOdenmemisDurumda", "Bu kayıt zaten ödenmemiş durumda.");
                return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/RegistrationsAndPayments?conferenceId={conference.Id}");
            }

            registration.IsPaid = false;
            registration.PaymentDate = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T("Msg_OdemeOnayiBasariylaGeriAlindi", "Ödeme onayı başarıyla geri alındı.");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/RegistrationsAndPayments?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow/RegistrationsAndPayments/Export")]
        public async Task<IActionResult> ExportRegistrations(
            string slug,
            Guid? conferenceId,
            string? search = null,
            string? paymentStatus = null,
            Guid? registrationTypeId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null) return Forbid();

            var query = _context.Registrations
                .AsNoTracking()
                .Include(x => x.AppUser)
                .Include(x => x.RegistrationType)
                .Where(x => x.ConferenceId == conference.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var t = search.Trim();
                query = query.Where(x =>
                    (x.AppUser != null && (
                        (x.AppUser.FirstName != null && x.AppUser.FirstName.Contains(t)) ||
                        (x.AppUser.LastName != null && x.AppUser.LastName.Contains(t)) ||
                        (x.AppUser.Email != null && x.AppUser.Email.Contains(t))
                    )) ||
                    (x.RegistrationType != null && x.RegistrationType.Name.Contains(t)) ||
                    (x.PaymentTransactionId != null && x.PaymentTransactionId.Contains(t))
                );
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                if (paymentStatus == "paid") query = query.Where(x => x.IsPaid);
                else if (paymentStatus == "pending") query = query.Where(x => !x.IsPaid);
            }

            if (registrationTypeId.HasValue && registrationTypeId.Value != Guid.Empty)
            {
                query = query.Where(x => x.RegistrationTypeId == registrationTypeId.Value);
            }

            var rows = await query
                .OrderByDescending(x => x.RegistrationDate)
                .Select(x => new
                {
                    FullName = (x.AppUser != null
                        ? ((x.AppUser.FirstName ?? "") + " " + (x.AppUser.LastName ?? "")).Trim()
                        : ""),
                    Email = x.AppUser != null ? x.AppUser.Email ?? "" : "",
                    RegistrationType = x.RegistrationType != null ? x.RegistrationType.Name : "",
                    x.Amount,
                    Currency = x.RegistrationType != null ? x.RegistrationType.Currency : "TRY",
                    x.IsPaid,
                    x.RegistrationDate,
                    x.PaymentDate,
                    x.PaymentTransactionId,
                    x.BillingName,
                    x.TaxNumber
                })
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ad Soyad,E-posta,Kayıt Türü,Tutar,Para Birimi,Ödeme Durumu,Kayıt Tarihi,Ödeme Tarihi,İşlem ID,Fatura Adı,Vergi No");

            foreach (var r in rows)
            {
                string Esc(string? v) => $"\"{(v ?? "").Replace("\"", "\"\"")}\"";
                sb.AppendLine($"{Esc(r.FullName)},{Esc(r.Email)},{Esc(r.RegistrationType)},{r.Amount:F2},{r.Currency},{(r.IsPaid ? "Ödendi" : "Bekliyor")},{r.RegistrationDate:yyyy-MM-dd HH:mm},{r.PaymentDate?.ToString("yyyy-MM-dd HH:mm") ?? ""},{Esc(r.PaymentTransactionId)},{Esc(r.BillingName)},{Esc(r.TaxNumber)}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            var fileName = $"kayitlar_{conference.Title?.Replace(" ", "_") ?? "export"}_{System.DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        [HttpGet("/Admin/ConferenceFlow/ProgramSessions")]
        public async Task<IActionResult> ProgramSessionsRoot(Guid? conferenceId)
        {
            return await RedirectToSelectedConferenceOperationAsync(
                conferenceId,
                nameof(ProgramSessions));
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow/ProgramSessions")]
        public async Task<IActionResult> ProgramSessions(string slug, Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidTenant",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            // SQLite ORDER BY icinde TimeSpan cevirmiyor (SQL Server cevirebiliyor).
            // Gun veritabaninda siralaniyor, gun ici sira bellekte tamamlaniyor.
            var sessions = (await _context.Sessions
                .AsNoTracking()
                .Include(x => x.Submissions)
                    .ThenInclude(x => x.Author)
                .Where(x => x.ConferenceId == conference.Id)
                .OrderBy(x => x.SessionDate)
                .ToListAsync())
                .OrderBy(x => x.SessionDate)
                .ThenBy(x => x.StartTime)
                .ThenBy(x => x.SortOrder)
                .ToList();

            var totalSessions = sessions.Count;

            var speakerCount = sessions
                .Where(x => !string.IsNullOrWhiteSpace(x.SpeakerName))
                .Select(x => x.SpeakerName!.Trim().ToLower())
                .Distinct()
                .Count();

            var hallCount = sessions
                .Where(x => !string.IsNullOrWhiteSpace(x.Location))
                .Select(x => x.Location!.Trim().ToLower())
                .Distinct()
                .Count();

            var programDayCount = sessions
                .Select(x => x.SessionDate.Date)
                .Distinct()
                .Count();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title ?? "";
            ViewBag.Slug = slug;

            ViewBag.TotalSessions = totalSessions;
            ViewBag.SpeakerCount = speakerCount;
            ViewBag.HallCount = hallCount;
            ViewBag.ProgramDayCount = programDayCount;

            return View("~/Areas/Admin/Views/ConferenceFlow/ProgramSessions.cshtml", sessions);
        }

        [HttpGet("/Admin/ConferenceFlow/ProgramSessions/Print")]
        public async Task<IActionResult> ProgramSessionsPrintRoot(Guid? conferenceId)
        {
            return await RedirectToProgramSessionsPrintAsync(conferenceId);
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow/ProgramSessions/Print")]
        public async Task<IActionResult> ProgramSessionsPrint(string slug, Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidTenant",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            // SQLite ORDER BY icinde TimeSpan cevirmiyor (SQL Server cevirebiliyor).
            // Gun veritabaninda siralaniyor, gun ici sira bellekte tamamlaniyor.
            var sessions = (await _context.Sessions
                .AsNoTracking()
                .Include(x => x.Submissions)
                    .ThenInclude(x => x.Author)
                .Where(x =>
                    x.ConferenceId == conference.Id &&
                    x.IsActive)
                .OrderBy(x => x.SessionDate)
                .ToListAsync())
                .OrderBy(x => x.SessionDate)
                .ThenBy(x => x.StartTime)
                .ThenBy(x => x.SortOrder)
                .ToList();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title ?? "";
            ViewBag.Slug = slug;

            return View("~/Areas/Admin/Views/ConferenceFlow/ProgramSessionsPrint.cshtml", sessions);
        }

        [HttpPost("/Admin/ConferenceFlow/ToggleBidding")]
        [HttpPost("/{slug}/Admin/ConferenceFlow/ToggleBidding")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBidding(string slug, Guid conferenceId)
        {
            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T("Msg_KongreBulunamadi", "Kongre bulunamadı.");
                return Redirect($"/{slug}/Admin/ConferenceFlow");
            }

            conference.IsBiddingOpen = !conference.IsBiddingOpen;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = conference.IsBiddingOpen
                ? T("Msg_TeklifFaziAcildiHakemlerBildiriTercihlerini", "Teklif fazı açıldı. Hakemler bildiri tercihlerini belirtebilir.")
                : T("Msg_TeklifFaziKapatildi", "Teklif fazı kapatıldı.");

            return Redirect($"/{slug}/Admin/ConferenceFlow");
        }

        [HttpPost("/Admin/ConferenceFlow/ToggleFullText")]
        [HttpPost("/{slug}/Admin/ConferenceFlow/ToggleFullText")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFullText(string slug, Guid conferenceId)
        {
            var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T("Msg_KongreBulunamadi", "Kongre bulunamadı.");
                return Redirect($"/{slug}/Admin/ConferenceFlow");
            }

            conference.IsFullTextOpen = !conference.IsFullTextOpen;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = conference.IsFullTextOpen
                ? T("Msg_TamMetinYuklemeAcildiYazarlarTam", "Tam metin yükleme açıldı. Yazarlar tam metin yükleyebilir.")
                : T("Msg_TamMetinYuklemeKapatildi", "Tam metin yükleme kapatıldı.");

            return Redirect($"/{slug}/Admin/ConferenceFlow");
        }

        [HttpGet("/ConferenceFlow/Index")]
        public IActionResult LegacyRoot()
        {
            // Sorgu dizesi korunmali: takma ad ayni icerige gidiyor.
            return Redirect($"/Admin/ConferenceFlow{Request.QueryString}");
        }

        [HttpGet("/{slug}/ConferenceFlow/Index")]
        public IActionResult LegacyTenant(string slug)
        {
            return Redirect($"/{slug}/Admin/ConferenceFlow");
        }
    }
}