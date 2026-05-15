using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels.Admin.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IStringLocalizer<DashboardController> _localizer;

        public DashboardController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            IStringLocalizer<DashboardController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _localizer = localizer;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> WhoAmI()
        {
            var user = await _userManager.GetUserAsync(User);

            var roles = new List<string>();

            if (user != null)
            {
                roles = (await _userManager.GetRolesAsync(user)).ToList();
            }

            return Json(new
            {
                userId = user?.Id,
                userName = user?.UserName,
                isAuthenticated = User.Identity?.IsAuthenticated,
                claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList(),
                roles
            });
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
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
            string? conferenceIdText = null;

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                conferenceIdText = HttpContext.Session.GetString(tenantKey);
            }

            conferenceIdText ??= HttpContext.Session.GetString("SelectedConferenceId");

            return Guid.TryParse(conferenceIdText, out var parsedId)
                ? parsedId
                : null;
        }

        private void SaveSelectedConference(
            Guid tenantId,
            Guid conferenceId,
            string selectedSlug,
            string? conferenceTitle = null)
        {
            HttpContext.Session.SetString("SelectedConferenceId", conferenceId.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", selectedSlug ?? "");
            HttpContext.Session.SetString("SelectedConferenceTitle", conferenceTitle ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conferenceId.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", selectedSlug ?? "");
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conferenceTitle ?? "");
        }

        private void ClearSelectedConference()
        {
            if (_tenantContext.Current != null)
            {
                HttpContext.Session.Remove($"SelectedConferenceId:{_tenantContext.Current.Id}");
                HttpContext.Session.Remove($"SelectedConferenceSlug:{_tenantContext.Current.Id}");
                HttpContext.Session.Remove($"SelectedConferenceTitle:{_tenantContext.Current.Id}");
            }

            HttpContext.Session.Remove("SelectedConferenceId");
            HttpContext.Session.Remove("SelectedConferenceSlug");
            HttpContext.Session.Remove("SelectedConferenceTitle");
        }

        private IQueryable<Guid> GetUserConferenceIds(string userId)
        {
            var registrationIds = _context.Registrations
                .AsNoTracking()
                .Where(r => r.AppUserId == userId)
                .Select(r => r.ConferenceId);

            var submissionIds = _context.Submissions
                .AsNoTracking()
                .Where(s => s.AuthorId == userId)
                .Select(s => s.ConferenceId);

            var reviewIds =
                from reviewAssignment in _context.ReviewAssignments.AsNoTracking()
                join submission in _context.Submissions.AsNoTracking()
                    on reviewAssignment.SubmissionId equals submission.Id
                where reviewAssignment.ReviewerId == userId
                select submission.ConferenceId;

            return registrationIds
                .Union(submissionIds)
                .Union(reviewIds);
        }

        private Task<List<Conference>> GetUserConferencesAsync(string userId)
        {
            var ids = GetUserConferenceIds(userId);

            return _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c => ids.Contains(c.Id))
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
        }

        private async Task<bool> UserCanAccessConferenceAsync(AppUser user, Guid conferenceId)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                return true;
            }

            var isOrganizator = await _userManager.IsInRoleAsync(user, "Organizator");

            if (isOrganizator)
            {
                if (!user.TenantId.HasValue)
                {
                    return false;
                }

                return await _context.Conferences
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == conferenceId &&
                        c.TenantId == user.TenantId.Value);
            }

            return await GetUserConferenceIds(user.Id)
                .AnyAsync(id => id == conferenceId);
        }

        private async Task<Conference?> GetSelectedConferenceForCurrentContextAsync(
            AppUser user,
            Guid conferenceId)
        {
            var canAccess = await UserCanAccessConferenceAsync(user, conferenceId);

            if (!canAccess)
            {
                return null;
            }

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c => c.Id == conferenceId);

            if (_tenantContext.Current != null)
            {
                query = query.Where(c => c.TenantId == _tenantContext.Current.Id);
            }

            return await query.FirstOrDefaultAsync();
        }

        private static bool IsAdminReturnUrl(string returnUrl)
        {
            var parts = returnUrl.Split('?', 2);
            var path = parts[0];

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                return false;
            }

            if (string.Equals(segments[0], "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (segments.Length > 1 &&
                string.Equals(segments[1], "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return path.Contains("/Admin/", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildAdminReturnUrl(
            string returnUrl,
            string newSlug,
            Guid conferenceId)
        {
            var parts = returnUrl.Split('?', 2);
            var path = parts[0];
            var queryString = parts.Length > 1 ? parts[1] : "";

            var segments = path
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (segments.Count > 0 &&
                string.Equals(segments[0], "Admin", StringComparison.OrdinalIgnoreCase))
            {
                segments.Insert(0, newSlug);
            }
            else if (segments.Count > 1 &&
                     string.Equals(segments[1], "Admin", StringComparison.OrdinalIgnoreCase))
            {
                segments[0] = newSlug;
            }

            var newPath = "/" + string.Join("/", segments);

            var parsedQuery = QueryHelpers.ParseQuery(
                string.IsNullOrWhiteSpace(queryString)
                    ? ""
                    : "?" + queryString);

            var dict = parsedQuery.ToDictionary(
                x => x.Key,
                x => x.Value.ToString());

            dict["conferenceId"] = conferenceId.ToString();

            return QueryHelpers.AddQueryString(newPath, dict);
        }

        [HttpGet]
        public IActionResult ChangeConference()
        {
            ClearSelectedConference();

            var slug = GetSlug();

            if (string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction(nameof(MyConferences));
            }

            return Redirect($"/{slug}/Dashboard/MyConferences");
        }

        [HttpGet]
        public IActionResult SelectConference(Guid conferenceId, string? returnUrl = null)
        {
            var slug = GetSlug();

            var effectiveReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? string.IsNullOrWhiteSpace(slug)
                    ? "/Dashboard"
                    : $"/{slug}/Dashboard"
                : returnUrl;

            if (!Url.IsLocalUrl(effectiveReturnUrl))
            {
                effectiveReturnUrl = string.IsNullOrWhiteSpace(slug)
                    ? "/Dashboard"
                    : $"/{slug}/Dashboard";
            }

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
                TempData["ErrorMessage"] = T(
                    "InvalidConferenceSelection",
                    "Geçersiz kongre seçimi.");

                return RedirectToAction(nameof(MyConferences));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conf == null)
            {
                TempData["ErrorMessage"] = T(
                    "ConferenceNotFound",
                    "Kongre bulunamadı.");

                return RedirectToAction(nameof(MyConferences));
            }

            var canAccess = await UserCanAccessConferenceAsync(user, conferenceId);

            if (!canAccess)
            {
                TempData["ErrorMessage"] = T(
                    "UnauthorizedConferenceAccess",
                    "Bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(MyConferences));
            }

            var selectedSlug = conf.Tenant?.Slug ?? conf.Slug ?? GetSlug();

            SaveSelectedConference(
                conf.TenantId,
                conf.Id,
                selectedSlug,
                conf.Title);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                if (IsAdminReturnUrl(returnUrl))
                {
                    return Redirect(BuildAdminReturnUrl(returnUrl, selectedSlug, conferenceId));
                }

                return Redirect(returnUrl);
            }

            if (!string.IsNullOrWhiteSpace(selectedSlug))
            {
                return Redirect($"/{selectedSlug}/Dashboard");
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isOrganizator = await _userManager.IsInRoleAsync(user, "Organizator");

            var selectedConferenceId = GetSelectedConferenceId();
            var slug = GetSlug();

            Conference? selectedConference = null;

            if (selectedConferenceId.HasValue)
            {
                selectedConference = await GetSelectedConferenceForCurrentContextAsync(
                    user,
                    selectedConferenceId.Value);

                if (selectedConference == null)
                {
                    ClearSelectedConference();

                    TempData["ErrorMessage"] = T(
                        "UnauthorizedPreviousConferenceAccess",
                        "Önceki seçili kongreye erişim yetkiniz yok veya kongre geçerli kurumla eşleşmiyor.");

                    return RedirectToAction(nameof(MyConferences));
                }

                SaveSelectedConference(
                    selectedConference.TenantId,
                    selectedConference.Id,
                    selectedConference.Tenant?.Slug ?? slug,
                    selectedConference.Title);
            }

            if (!selectedConferenceId.HasValue)
            {
                if (isOrganizator && !isAdmin && user.TenantId.HasValue)
                {
                    var autoConference = await _context.Conferences
                        .Include(c => c.Tenant)
                        .Where(c => c.TenantId == user.TenantId.Value)
                        .OrderByDescending(c => c.StartDate)
                        .FirstOrDefaultAsync();

                    if (autoConference != null)
                    {
                        var selectedSlug = autoConference.Tenant?.Slug ?? slug;

                        SaveSelectedConference(
                            autoConference.TenantId,
                            autoConference.Id,
                            selectedSlug,
                            autoConference.Title);

                        return Redirect($"/{selectedSlug}/Dashboard");
                    }
                }

                return RedirectToAction(nameof(MyConferences));
            }

            var submissionsQuery = _context.Submissions
                .AsQueryable()
                .Where(s => s.AuthorId == user.Id);

            if (selectedConferenceId.HasValue)
            {
                submissionsQuery = submissionsQuery
                    .Where(s => s.ConferenceId == selectedConferenceId.Value);
            }

            var reviewAssignmentsQuery = _context.ReviewAssignments
                .AsQueryable()
                .Where(ra => ra.ReviewerId == user.Id);

            if (selectedConferenceId.HasValue)
            {
                reviewAssignmentsQuery = reviewAssignmentsQuery
                    .Where(ra => ra.Submission.ConferenceId == selectedConferenceId.Value);
            }

            var pendingReviews = await reviewAssignmentsQuery
                .CountAsync(ra => ra.Review == null);

            var completedReviews = await reviewAssignmentsQuery
                .CountAsync(ra => ra.Review != null);

            var isReferee =
                await _userManager.IsInRoleAsync(user, "Referee") ||
                await _userManager.IsInRoleAsync(user, "Admin") ||
                await reviewAssignmentsQuery.AnyAsync();

            ViewBag.IsReferee = isReferee;
            ViewBag.PendingReviews = pendingReviews;
            ViewBag.CompletedReviews = completedReviews;

            ViewBag.IsAuthor =
                await _userManager.IsInRoleAsync(user, "Author") ||
                await submissionsQuery.AnyAsync();

            var myConferences = await GetUserConferencesAsync(user.Id);

            var currentConferenceName = T(
                "GeneralManagementPanel",
                "Genel Yönetim Paneli");

            if (selectedConference != null)
            {
                currentConferenceName = selectedConference.Title ?? currentConferenceName;
            }
            else if (_tenantContext.Current != null)
            {
                currentConferenceName = _tenantContext.Current.Name;
            }

            var viewModel = new DashboardViewModel
            {
                TotalSubmissions = await submissionsQuery.CountAsync(),

                AcceptedSubmissions = await submissionsQuery
                    .CountAsync(s =>
                        s.Status == SubmissionStatus.Accepted ||
                        s.Status == SubmissionStatus.Presented),

                AwaitingDecision = await submissionsQuery
                    .CountAsync(s =>
                        s.Status == SubmissionStatus.New ||
                        s.Status == SubmissionStatus.UnderReview ||
                        s.Status == SubmissionStatus.RevisionRequired),

                RejectedSubmissions = await submissionsQuery
                    .CountAsync(s => s.Status == SubmissionStatus.Rejected),

                ConferenceName = currentConferenceName,
                MyConferences = myConferences
            };

            return View(viewModel);
        }

        public async Task<IActionResult> MyConferences()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isOrganizator = await _userManager.IsInRoleAsync(user, "Organizator");

            if (isOrganizator && !isAdmin)
            {
                if (!user.TenantId.HasValue)
                {
                    TempData["ErrorMessage"] = T(
                        "NoInstitutionAssigned",
                        "Hesabınıza bağlı kurum bulunamadı.");
                }
                else
                {
                    var autoConference = await _context.Conferences
                        .AsNoTracking()
                        .Include(c => c.Tenant)
                        .Where(c => c.TenantId == user.TenantId.Value)
                        .OrderByDescending(c => c.StartDate)
                        .FirstOrDefaultAsync();

                    if (autoConference != null)
                    {
                        var selectedSlug =
                            autoConference.Tenant?.Slug ??
                            _tenantContext.Current?.Slug ??
                            autoConference.Slug ??
                            "";

                        SaveSelectedConference(
                            autoConference.TenantId,
                            autoConference.Id,
                            selectedSlug,
                            autoConference.Title);

                        if (!string.IsNullOrWhiteSpace(selectedSlug))
                        {
                            return Redirect($"/{selectedSlug}/Dashboard");
                        }

                        return RedirectToAction(nameof(Index));
                    }

                    TempData["InfoMessage"] = T(
                        "NoConferenceAssignedToInstitution",
                        "Kurumunuza bağlı kongre bulunamadı.");
                }
            }

            List<Conference> registeredConferences;
            List<Conference> availableConferences;

            if (isAdmin)
            {
                registeredConferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .OrderByDescending(c => c.StartDate)
                    .ToListAsync();

                availableConferences = new List<Conference>();
            }
            else if (isOrganizator)
            {
                if (user.TenantId.HasValue)
                {
                    registeredConferences = await _context.Conferences
                        .AsNoTracking()
                        .Include(c => c.Tenant)
                        .Where(c => c.TenantId == user.TenantId.Value)
                        .OrderByDescending(c => c.StartDate)
                        .ToListAsync();
                }
                else
                {
                    registeredConferences = new List<Conference>();
                }

                availableConferences = new List<Conference>();
            }
            else
            {
                var myConferenceIds = await GetUserConferenceIds(user.Id)
                    .Distinct()
                    .ToListAsync();

                registeredConferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Where(c => myConferenceIds.Contains(c.Id))
                    .OrderByDescending(c => c.StartDate)
                    .ToListAsync();

                availableConferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Where(c =>
                        c.EndDate.Date >= DateTime.Today &&
                        !myConferenceIds.Contains(c.Id))
                    .OrderBy(c => c.StartDate)
                    .ToListAsync();
            }

            ViewBag.AvailableConferences = availableConferences;

            return View(registeredConferences);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomLogout(
            [FromServices] SignInManager<AppUser> signInManager)
        {
            HttpContext.Session.Clear();

            await signInManager.SignOutAsync();

            return Redirect("/");
        }
    }
}