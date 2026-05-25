using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Decision;
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
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<DecisionController> _localizer;

        public DecisionController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<DecisionController> localizer)
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

            return user?.TenantId;
        }

        private async Task<bool> CurrentAdminHasTenantAsync()
        {
            if (IsSuperAdminUser())
            {
                return true;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            return tenantId.HasValue && tenantId.Value != Guid.Empty;
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

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return false;
            }

            return tenantId.Value == _tenantContext.Current.Id;
        }

        private static bool SlugMatches(Conference? conference, string? slug)
        {
            if (conference == null || string.IsNullOrWhiteSpace(slug))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(conference.Slug) &&
                string.Equals(conference.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug) &&
                string.Equals(conference.Tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string GetCanonicalSlug(Conference? conference, string? fallbackSlug = null)
        {
            return conference?.Tenant?.Slug
                   ?? conference?.Slug
                   ?? fallbackSlug
                   ?? "";
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

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return query.Where(c => false);
            }

            return query.Where(c => c.TenantId == tenantId.Value);
        }

        private async Task<Conference?> GetAccessibleConferenceAsync(
            string slug,
            Guid? conferenceId)
        {
            Guid? selectedConferenceId;

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

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                var conference = await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value);

                if (conference == null || !SlugMatches(conference, slug))
                {
                    return null;
                }

                return conference;
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

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = GetCanonicalSlug(conference, _tenantContext.Current?.Slug);
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        private string BuildDecisionUrl(string slug, Guid conferenceId)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return $"/Admin/Decision?conferenceId={conferenceId}";
            }

            return $"/{slug}/Admin/Decision?conferenceId={conferenceId}";
        }

        private async Task<Submission?> GetAccessibleSubmissionForDecisionAsync(
            string slug,
            Guid submissionId)
        {
            var query = _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                var submission = await query.FirstOrDefaultAsync(s =>
                    s.Id == submissionId &&
                    s.Conference != null);

                if (submission == null || !SlugMatches(submission.Conference, slug))
                {
                    return null;
                }

                return submission;
            }

            if (_tenantContext.Current == null)
            {
                return null;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return null;
            }

            return await query.FirstOrDefaultAsync(s =>
                s.Id == submissionId &&
                s.Conference != null &&
                s.Conference.TenantId == tenantId.Value &&
                s.Conference.Tenant != null &&
                s.Conference.Tenant.Slug == slug);
        }

        [HttpGet("/Admin/Decision")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            if (!await CurrentAdminHasTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            var selectedId = GetSelectedConferenceId(_tenantContext.Current?.Id);

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConference?.Tenant != null &&
                    !string.IsNullOrWhiteSpace(GetCanonicalSlug(selectedConference)))
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect(BuildDecisionUrl(
                        GetCanonicalSlug(selectedConference),
                        selectedConference.Id));
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            if (!conferences.Any())
            {
                TempData["ErrorMessage"] = IsSuperAdminUser()
                    ? T("Error_NoConferenceForSuperAdmin", "Sistemde görüntülenebilecek kongre bulunamadı.")
                    : T("Error_NoConferenceForAdmin", "Kurumunuza bağlı görüntülenebilecek kongre bulunamadı.");
            }

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = IsSuperAdminUser()
                    ? T("SelectConference_SuperAdminLead", "SuperAdmin olarak sistemdeki tüm kongreleri görebilirsiniz. Karar ekranını incelemek istediğiniz kongreyi seçiniz.")
                    : T("SelectConference_Lead", "Karar ekranını görüntülemek için önce kongre seçiniz."),
                PostUrl = "/Admin/Decision/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Decision/Select")]
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

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            var canonicalSlug = GetCanonicalSlug(conference);

            if (conference == null || string.IsNullOrWhiteSpace(canonicalSlug))
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

            return Redirect(BuildDecisionUrl(canonicalSlug, conference.Id));
        }

        [HttpGet("/{slug}/Admin/Decision")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Decision" });
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(BuildDecisionUrl(canonicalSlug, conference.Id));
            }

            SetSelectedConferenceSession(conference);

            var allSubmissions = _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Reviewer)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Review)
                .OrderByDescending(s => s.CreatedDate)
                .AsQueryable();

            var awaitingDecision = await allSubmissions
                .Where(s =>
                    s.Status == SubmissionStatus.Pending ||
                    s.Status == SubmissionStatus.UnderReview)
                .ToListAsync();

            var decided = await allSubmissions
                .Where(s =>
                    s.Status == SubmissionStatus.Accepted ||
                    s.Status == SubmissionStatus.Rejected ||
                    s.Status == SubmissionStatus.RevisionRequired)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = canonicalSlug;

            var viewModel = new DecisionIndexViewModel
            {
                AwaitingDecision = awaitingDecision,
                AlreadyDecided = decided
            };

            return View("~/Areas/Admin/Views/Decision/Index.cshtml", viewModel);
        }

        [HttpGet("/Decision/Index")]
        public IActionResult LegacyRoot()
        {
            return Redirect("/Admin/Decision");
        }

        [HttpGet("/{slug}/Decision/Index")]
        public IActionResult LegacyTenant(string slug)
        {
            return Redirect($"/{slug}/Admin/Decision");
        }

        [HttpPost("/{slug}/Admin/Decision/MakeDecision")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(
            string slug,
            Guid submissionId,
            string decision,
            string? note = null)
        {
            var submission = await GetAccessibleSubmissionForDecisionAsync(
                slug,
                submissionId);

            if (submission == null || submission.Conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionNotLinkedToTenantConference",
                    "Bildiri bulunamadı veya bu bildiriye karar verme yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Decision");
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == submission.ConferenceId);

            if (conference == null || !SlugMatches(conference, slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return Redirect("/Admin/Decision");
            }

            if (!IsSuperAdminUser())
            {
                var adminTenantId = await GetCurrentAdminTenantIdAsync();

                if (!adminTenantId.HasValue ||
                    adminTenantId.Value == Guid.Empty ||
                    conference.TenantId != adminTenantId.Value)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_TenantMismatch",
                        "Bu kongre için karar verme yetkiniz yok.");

                    return RedirectToAction(nameof(SelectConference));
                }
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference);

            string decisionText;

            if (decision == "Accept")
            {
                submission.Status = SubmissionStatus.Accepted;
                decisionText = T("Decision_Accepted", "Kabul Edildi");
            }
            else if (decision == "Reject")
            {
                submission.Status = SubmissionStatus.Rejected;
                decisionText = T("Decision_Rejected", "Reddedildi");
            }
            else if (decision == "Revision")
            {
                submission.Status = SubmissionStatus.RevisionRequired;
                decisionText = T("Decision_RevisionRequested", "Revizyon İstendi");
            }
            else
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidDecision",
                    "Geçersiz karar seçimi.");

                return Redirect(BuildDecisionUrl(canonicalSlug, submission.ConferenceId));
            }

            submission.DecisionDate = DateTime.UtcNow;
            submission.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_SubmissionDecisionSaved",
                $"Bildiri kararı kaydedildi: {decisionText}");

            return Redirect(BuildDecisionUrl(canonicalSlug, submission.ConferenceId));
        }
    }
}