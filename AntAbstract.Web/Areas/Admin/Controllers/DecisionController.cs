using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Decision;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

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

        [HttpGet("/Admin/Decision")]
        public async Task<IActionResult> SelectConference()
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var selectedConf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", selectedConf.Tenant.Slug);
                    return Redirect($"/{selectedConf.Tenant.Slug}/Admin/Decision?conferenceId={selectedConf.Id}");
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
                Title = _localizer["SelectConference_Title"],
                Lead = _localizer["SelectConference_Lead"],
                PostUrl = "/Admin/Decision/Select",
                SubmitText = _localizer["SelectConference_Submit"],
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Decision/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId)
        {
            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = _localizer["Error_ConferenceNotFound"];
                return Redirect("/Admin/Decision");
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Admin/Decision?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/Decision")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_SelectConferenceFirst"];
                return Redirect("/Admin/Decision");
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = _localizer["Error_TenantMismatchSelectAgain"];
                return Redirect("/Admin/Decision");
            }

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();
            if (conferenceId == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_SelectConferenceFirst"];
                return Redirect("/Admin/Decision");
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_ConferenceNotFoundForTenant"];
                return Redirect("/Admin/Decision");
            }

            var allSubmissions = _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Reviewer)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Review)
                .OrderByDescending(s => s.CreatedDate)
                .AsQueryable();

            var awaitingDecision = await allSubmissions
                .Where(s => s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.UnderReview)
                .ToListAsync();

            var decided = await allSubmissions
                .Where(s => s.Status == SubmissionStatus.Accepted
                         || s.Status == SubmissionStatus.Rejected
                         || s.Status == SubmissionStatus.RevisionRequired)
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
        public IActionResult LegacyRoot() => Redirect("/Admin/Decision");

        [HttpGet("/{slug}/Decision/Index")]
        public IActionResult LegacyTenant(string slug) => Redirect($"/{slug}/Admin/Decision");

        [HttpPost("/{slug}/Admin/Decision/MakeDecision")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(string slug, Guid submissionId, string decision, string note)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_SelectConferenceFirst"];
                return Redirect("/Admin/Decision");
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = _localizer["Error_TenantMismatch"];
                return Redirect("/Admin/Decision");
            }

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null)
                return NotFound();

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == submission.ConferenceId && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_SubmissionNotLinkedToTenantConference"];
                return Redirect("/Admin/Decision");
            }

            string decisionText;

            if (decision == "Accept")
            {
                submission.Status = SubmissionStatus.Accepted;
                decisionText = _localizer["Decision_Accepted"];
            }
            else if (decision == "Reject")
            {
                submission.Status = SubmissionStatus.Rejected;
                decisionText = _localizer["Decision_Rejected"];
            }
            else
            {
                submission.Status = SubmissionStatus.RevisionRequired;
                decisionText = _localizer["Decision_RevisionRequested"];
            }

            submission.DecisionDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["Success_SubmissionDecisionSaved", decisionText];
            return Redirect($"/{slug}/Admin/Decision?conferenceId={submission.ConferenceId}");
        }
    }
}