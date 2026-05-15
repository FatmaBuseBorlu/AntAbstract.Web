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
    [Authorize(Roles = "Admin,Organizator")]
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

            return value.ResourceNotFound
                ? fallback
                : value.Value;
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

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
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

            return query;
        }

        private async Task<Conference?> GetAccessibleConferenceAsync(string slug, Guid? conferenceId)
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

            if (selectedConferenceId == null || selectedConferenceId.Value == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);
        }

        private async Task<bool> CanAccessSubmissionAsync(Guid submissionId)
        {
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

            if (!user.TenantId.HasValue)
            {
                return false;
            }

            return await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Conference)
                .AnyAsync(s =>
                    s.Id == submissionId &&
                    s.Conference != null &&
                    s.Conference.TenantId == user.TenantId.Value);
        }

        [HttpGet("/Admin/Decision")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId != null)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConf = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", selectedConf.Tenant.Slug);
                    HttpContext.Session.SetString("SelectedConferenceTitle", selectedConf.Title ?? "");

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect($"/{selectedConf.Tenant.Slug}/Admin/Decision?conferenceId={selectedConf.Id}");
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
            var query = await GetAccessibleConferenceQueryAsync();

            var conf = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect($"/{conf.Tenant.Slug}/Admin/Decision?conferenceId={conf.Id}");
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

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

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