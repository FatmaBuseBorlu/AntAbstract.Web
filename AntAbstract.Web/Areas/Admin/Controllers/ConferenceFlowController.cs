using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Assignment;
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
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ConferenceFlowController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<ConferenceFlowController> _localizer;

        public ConferenceFlowController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<ConferenceFlowController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
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
            return User.IsInRole("SuperAdmin");
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
            if (IsSuperAdminUser())
            {
                return true;
            }

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
            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                return query;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return query.Where(c => false);
            }

            return query.Where(c => c.TenantId == tenantId.Value);
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
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetConferenceSession(conference);

            return Redirect($"/{conference.Tenant.Slug}/Admin/ConferenceFlow/{operationAction}?conferenceId={conference.Id}");
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
                    ? "Sistemde görüntülenebilecek kongre bulunamadı."
                    : "Kurumunuza bağlı görüntülenebilecek kongre bulunamadı.";
            }

            var vm = new SelectConferenceViewModel
            {
                Title = "Kongre Akışı İçin Kongre Seç",
                Lead = IsSuperAdminUser()
                    ? "SuperAdmin olarak sistemdeki tüm kongreleri görebilirsiniz. Akışını incelemek istediğiniz kongreyi seçiniz."
                    : "Kongre akışını görüntülemek için kongre seçiniz.",
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
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conf = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectedConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

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

            var vm = new ConferenceFlowIndexViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                Slug = slug,
                SubmissionCount = submissionCount,
                AssignedSubmissionCount = assignedSubmissionCount,
                DecidedSubmissionCount = decidedSubmissionCount
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
        public async Task<IActionResult> RegistrationsAndPayments(string slug, Guid? conferenceId)
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

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title ?? "";
            ViewBag.Slug = slug;

            return View("~/Areas/Admin/Views/ConferenceFlow/RegistrationsAndPayments.cshtml");
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

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title ?? "";
            ViewBag.Slug = slug;

            return View("~/Areas/Admin/Views/ConferenceFlow/ProgramSessions.cshtml");
        }

        [HttpGet("/ConferenceFlow/Index")]
        public IActionResult LegacyRoot()
        {
            return Redirect("/Admin/ConferenceFlow");
        }

        [HttpGet("/{slug}/ConferenceFlow/Index")]
        public IActionResult LegacyTenant(string slug)
        {
            return Redirect($"/{slug}/Admin/ConferenceFlow");
        }
    }
}