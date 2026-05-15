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
    [Authorize(Roles = "Admin")]
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

        private async Task<bool> CanAccessSubmissionAsync(Guid submissionId)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return false;
            }

            return await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Conference)
                .AnyAsync(s =>
                    s.Id == submissionId &&
                    s.Conference != null &&
                    s.Conference.TenantId == tenantId.Value);
        }

        [HttpGet("/Admin/Decision")]
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
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConference?.Tenant?.Slug != null)
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect($"/{selectedConference.Tenant.Slug}/Admin/Decision?conferenceId={selectedConference.Id}");
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = T("SelectConference_Lead", "Karar ekranını görüntülemek için önce kongre seçiniz."),
                PostUrl = "/Admin/Decision/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Decision/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
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

            return Redirect($"/{conference.Tenant.Slug}/Admin/Decision?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/Decision")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
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
            ViewBag.Slug = slug;

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
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
                !await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_TenantMismatch",
                    "Bu kongre için karar verme yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            if (!await CanAccessSubmissionAsync(submissionId))
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionNotLinkedToTenantConference",
                    "Bildiri bulunamadı veya bu bildiriye karar verme yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Decision");
            }

            var submission = await _context.Submissions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s =>
                    s.Id == submissionId &&
                    s.Conference != null &&
                    s.Conference.TenantId == _tenantContext.Current.Id);

            if (submission == null)
            {
                return NotFound();
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Id == submission.ConferenceId &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return NotFound();
            }

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

                return Redirect($"/{slug}/Admin/Decision?conferenceId={submission.ConferenceId}");
            }

            submission.DecisionDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_SubmissionDecisionSaved",
                $"Bildiri kararı kaydedildi: {decisionText}");

            return Redirect($"/{slug}/Admin/Decision?conferenceId={submission.ConferenceId}");
        }
    }
}