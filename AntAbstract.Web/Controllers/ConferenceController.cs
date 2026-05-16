using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class ConferenceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IStringLocalizer<ConferenceController> _localizer;

        public ConferenceController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            ISelectedConferenceService selectedConferenceService,
            IStringLocalizer<ConferenceController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _selectedConferenceService = selectedConferenceService;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync(AppUser user)
        {
            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (isSuperAdmin)
            {
                return query.Where(c => false);
            }

            if (isAdmin)
            {
                if (!user.TenantId.HasValue)
                {
                    return query.Where(c => false);
                }

                return query.Where(c => c.TenantId == user.TenantId.Value);
            }

            var registrationConferenceIds = _context.Registrations
                .AsNoTracking()
                .Where(r => r.AppUserId == user.Id)
                .Select(r => r.ConferenceId);

            var authorConferenceIds = _context.Submissions
                .AsNoTracking()
                .Where(s => s.AuthorId == user.Id)
                .Select(s => s.ConferenceId);

            var refereeConferenceIds = _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => ra.ReviewerId == user.Id)
                .Join(
                    _context.Submissions.AsNoTracking(),
                    reviewAssignment => reviewAssignment.SubmissionId,
                    submission => submission.Id,
                    (reviewAssignment, submission) => submission.ConferenceId);

            var allowedConferenceIds = registrationConferenceIds
                .Union(authorConferenceIds)
                .Union(refereeConferenceIds)
                .Distinct();

            return query.Where(c => allowedConferenceIds.Contains(c.Id));
        }

        private void SetConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? "";
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        private void ClearConferenceSession()
        {
            _selectedConferenceService.ClearSelectedConferenceId();

            HttpContext.Session.Remove("SelectedConferenceId");
            HttpContext.Session.Remove("SelectedConferenceSlug");
            HttpContext.Session.Remove("SelectedConferenceTitle");
        }

        [AllowAnonymous]
        [HttpGet("Conference/Details/{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return NotFound(T(
                    "ConferenceNotSpecified",
                    "Kongre belirtilmedi."));
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Include(c => c.Registrations)
                .FirstOrDefaultAsync(c => c.Slug == slug);

            if (conference == null && Guid.TryParse(slug, out var conferenceId))
            {
                conference = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Include(c => c.Registrations)
                    .FirstOrDefaultAsync(c => c.Id == conferenceId);
            }

            if (conference == null)
            {
                return NotFound(T(
                    "ConferenceNotFound",
                    "Kongre bulunamadı."));
            }

            var culture = CultureInfo.CurrentUICulture.Name;

            var pageBlocks = await _context.ConferencePageBlocks
                .AsNoTracking()
                .Where(b =>
                    b.ConferenceId == conference.Id &&
                    b.TenantId == conference.TenantId &&
                    b.IsActive &&
                    b.Page == "Home" &&
                    (
                        b.Culture == culture ||
                        string.IsNullOrEmpty(b.Culture)
                    ))
                .OrderBy(b => b.Order)
                .ToListAsync();

            ViewBag.PageBlocks = pageBlocks;

            return View(conference);
        }

        [HttpGet("/Conference/Select")]
        public async Task<IActionResult> Select()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Redirect("/Identity/Account/Login");
            }

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");

            if (isSuperAdmin)
            {
                ClearConferenceSession();
                return RedirectToAction("SuperAdmin", "Dashboard");
            }

            var query = await GetAccessibleConferenceQueryAsync(user);

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConferenceTitle", "Kongre Seç"),
                Lead = T("SelectConferenceLead", "Devam etmek için bir kongre seçiniz."),
                PostUrl = "/Conference/Select",
                SubmitText = T("ContinueButton", "Devam Et"),
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Conference/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectPost(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = T(
                    "ConferenceNotFoundShort",
                    "Kongre bulunamadı.");

                return Redirect("/Conference/Select");
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Redirect("/Identity/Account/Login");
            }

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");

            if (isSuperAdmin)
            {
                ClearConferenceSession();
                return RedirectToAction("SuperAdmin", "Dashboard");
            }

            var query = await GetAccessibleConferenceQueryAsync(user);

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null ||
                conference.Tenant == null ||
                string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "ConferenceNotFoundShort",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return Redirect("/Conference/Select");
            }

            SetConferenceSession(conference);

            return Redirect($"/{conference.Tenant.Slug}/Dashboard?conferenceId={conference.Id}");
        }
    }
}