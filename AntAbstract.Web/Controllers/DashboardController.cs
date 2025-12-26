using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public DashboardController(AppDbContext context, UserManager<AppUser> userManager, TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> WhoAmI()
        {
            var user = await _userManager.GetUserAsync(User);

            var roles = new List<string>();
            if (user != null)
                roles = (await _userManager.GetRolesAsync(user)).ToList();

            return Json(new
            {
                userId = user?.Id,
                userName = user?.UserName,
                isAuthenticated = User.Identity?.IsAuthenticated,
                claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList(),
                roles
            });
        }

        private string GetSlug()
        {
            return RouteData.Values["slug"]?.ToString()
                   ?? _tenantContext.Current?.Slug
                   ?? HttpContext.Session.GetString("SelectedConferenceSlug")
                   ?? "";
        }

        private Guid? GetSelectedConferenceId()
        {
            string? confIdStr = null;

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                confIdStr = HttpContext.Session.GetString(tenantKey);
            }

            confIdStr ??= HttpContext.Session.GetString("SelectedConferenceId");

            return Guid.TryParse(confIdStr, out var parsedId) ? parsedId : null;
        }

        private void SaveSelectedConference(Guid conferenceId, string selectedSlug)
        {
            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                HttpContext.Session.SetString(tenantKey, conferenceId.ToString());
            }

            HttpContext.Session.SetString("SelectedConferenceId", conferenceId.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", selectedSlug ?? "");
        }

        private IQueryable<Guid> GetUserConferenceIds(string userId)
        {
            var regIds = _context.Registrations
                .AsNoTracking()
                .Where(r => r.AppUserId == userId)
                .Select(r => r.ConferenceId);

            var submissionIds = _context.Submissions
                .AsNoTracking()
                .Where(s => s.AuthorId == userId)
                .Select(s => s.ConferenceId);

            var reviewIds = _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => ra.ReviewerId == userId)
                .Select(ra => ra.Submission.ConferenceId);

            return regIds.Union(submissionIds).Union(reviewIds);
        }

        private Task<List<Conference>> GetUserConferencesAsync(string userId)
        {
            var ids = GetUserConferenceIds(userId);

            return _context.Conferences
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
        }

        [HttpGet]
        public IActionResult SelectConference(Guid conferenceId, string? returnUrl = null)
        {
            var slug = GetSlug();
            var effectiveReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? (string.IsNullOrWhiteSpace(slug) ? "/Dashboard" : $"/{slug}/Dashboard")
                : returnUrl;

            ViewBag.ConferenceId = conferenceId;
            ViewBag.ReturnUrl = effectiveReturnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "Kongre seçimi geçersiz.";
                return RedirectToAction(nameof(MyConferences), new { slug = GetSlug() });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                var allowed = await GetUserConferenceIds(user.Id).AnyAsync(x => x == conferenceId);
                if (!allowed)
                {
                    TempData["ErrorMessage"] = "Bu kongreye erişim yok.";
                    return RedirectToAction(nameof(MyConferences), new { slug = GetSlug() });
                }
            }

            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conf == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(MyConferences), new { slug = GetSlug() });
            }

            var selectedSlug = conf.Tenant?.Slug ?? conf.Slug ?? GetSlug();
            SaveSelectedConference(conferenceId, selectedSlug);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (!string.IsNullOrWhiteSpace(selectedSlug))
                return Redirect($"/{selectedSlug}/Dashboard");

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var selectedConferenceId = GetSelectedConferenceId();
            var slug = GetSlug();

            if (!isAdmin && !selectedConferenceId.HasValue)
                return RedirectToAction(nameof(MyConferences), new { slug });

            var submissionsQuery = _context.Submissions.AsQueryable()
                .Where(s => s.AuthorId == user.Id);

            if (selectedConferenceId.HasValue)
                submissionsQuery = submissionsQuery.Where(s => s.ConferenceId == selectedConferenceId.Value);

            var reviewAssignmentsQuery = _context.ReviewAssignments.AsQueryable()
                .Where(ra => ra.ReviewerId == user.Id);

            if (selectedConferenceId.HasValue)
                reviewAssignmentsQuery = reviewAssignmentsQuery.Where(ra => ra.Submission.ConferenceId == selectedConferenceId.Value);

            var pendingReviews = await reviewAssignmentsQuery.CountAsync(ra => ra.Review == null);
            var completedReviews = await reviewAssignmentsQuery.CountAsync(ra => ra.Review != null);

            var isReferee = (await _userManager.IsInRoleAsync(user, "Referee"))
                            || (await _userManager.IsInRoleAsync(user, "Admin"))
                            || (await reviewAssignmentsQuery.AnyAsync());

            ViewBag.IsReferee = isReferee;
            ViewBag.PendingReviews = pendingReviews;
            ViewBag.CompletedReviews = completedReviews;

            ViewBag.IsAuthor = (await _userManager.IsInRoleAsync(user, "Author")) || (await submissionsQuery.AnyAsync());

            var myConferences = await GetUserConferencesAsync(user.Id);

            var currentConferenceName = "Genel Yönetim Paneli";
            if (selectedConferenceId.HasValue)
            {
                var selectedTitle = await _context.Conferences
                    .AsNoTracking()
                    .Where(x => x.Id == selectedConferenceId.Value)
                    .Select(x => x.Title)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(selectedTitle))
                    currentConferenceName = selectedTitle;
                else if (_tenantContext.Current != null)
                    currentConferenceName = _tenantContext.Current.Name;
            }
            else if (_tenantContext.Current != null)
            {
                currentConferenceName = _tenantContext.Current.Name;
            }

            var viewModel = new DashboardViewModel
            {
                TotalSubmissions = await submissionsQuery.CountAsync(),
                AcceptedSubmissions = await submissionsQuery.CountAsync(s => s.Status == SubmissionStatus.Accepted || s.Status == SubmissionStatus.Presented),
                AwaitingDecision = await submissionsQuery.CountAsync(s => s.Status == SubmissionStatus.New || s.Status == SubmissionStatus.UnderReview || s.Status == SubmissionStatus.RevisionRequired),
                RejectedSubmissions = await submissionsQuery.CountAsync(s => s.Status == SubmissionStatus.Rejected),
                ConferenceName = currentConferenceName,
                MyConferences = myConferences
            };

            return View(viewModel);
        }

        public async Task<IActionResult> MyConferences()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var conferences = await GetUserConferencesAsync(user.Id);
            return View(conferences);
        }
    }
}
