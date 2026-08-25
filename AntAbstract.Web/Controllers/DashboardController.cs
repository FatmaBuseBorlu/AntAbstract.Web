using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels.Admin.Dashboard;
using AntAbstract.Web.Security;
using AntAbstract.Web.Models.ViewModels.Proceedings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IStringLocalizer<DashboardController> _localizer;
        private readonly IConfiguration _configuration;

        private static readonly string[] ReviewerRoleNames =
        {
            "Referee",
            "Hakem",
            "Reviewer"
        };

        private static readonly string[] AuthorRoleNames =
        {
            "Author",
            "Yazar"
        };

        private static readonly string[] ListenerRoleNames =
        {
            "Listener",
            "Dinleyici"
        };

        public DashboardController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            TenantContext tenantContext,
            IStringLocalizer<DashboardController> localizer,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _tenantContext = tenantContext;
            _localizer = localizer;
            _configuration = configuration;
        }

        [Authorize(Policy = AdminPolicies.TenantAdmin)]
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

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SuperAdmin()
        {
            ClearSelectedConference();

            var now = DateTime.UtcNow;

            // --- Temel sayılar ---
            ViewBag.TotalTenants = await _context.Tenants.AsNoTracking().CountAsync();
            ViewBag.TotalConferences = await _context.Conferences.AsNoTracking().CountAsync();
            ViewBag.TotalUsers = await _context.Users.AsNoTracking().CountAsync();
            ViewBag.TotalSubmissions = await _context.Submissions.AsNoTracking().CountAsync();
            ViewBag.TotalRegistrations = await _context.Registrations.AsNoTracking().CountAsync();
            ViewBag.TotalCertificates = await _context.Certificates.AsNoTracking().CountAsync();

            // --- Aktif kongreler (bugün başladı/henüz bitmedi) ---
            ViewBag.ActiveConferences = await _context.Conferences
                .AsNoTracking()
                .CountAsync(c => c.StartDate <= now && c.EndDate >= now);

            // --- Rol bazlı kullanıcı sayıları ---
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            var authorRole = await _roleManager.FindByNameAsync("Author");
            var refereeRole = await _roleManager.FindByNameAsync("Referee");
            var listenerRole = await _roleManager.FindByNameAsync("Listener");

            ViewBag.TotalAdmins = adminRole != null ? (await _userManager.GetUsersInRoleAsync("Admin")).Count : 0;
            ViewBag.TotalAuthors = authorRole != null ? (await _userManager.GetUsersInRoleAsync("Author")).Count : 0;
            ViewBag.TotalReferees = refereeRole != null ? (await _userManager.GetUsersInRoleAsync("Referee")).Count : 0;
            ViewBag.TotalListeners = listenerRole != null ? (await _userManager.GetUsersInRoleAsync("Listener")).Count : 0;

            // --- Bildiri durum dağılımı ---
            var submissionStats = await _context.Submissions
                .AsNoTracking()
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.SubmissionAccepted = submissionStats.FirstOrDefault(x => x.Status == SubmissionStatus.Accepted)?.Count ?? 0;
            ViewBag.SubmissionRejected = submissionStats.FirstOrDefault(x => x.Status == SubmissionStatus.Rejected)?.Count ?? 0;
            ViewBag.SubmissionPending = submissionStats.FirstOrDefault(x => x.Status == SubmissionStatus.Pending)?.Count ?? 0;
            ViewBag.SubmissionUnderReview = submissionStats.FirstOrDefault(x => x.Status == SubmissionStatus.UnderReview)?.Count ?? 0;

            // --- Son 5 kongre ---
            ViewBag.RecentConferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .Take(5)
                .ToListAsync();

            // --- Son 5 kayıt ---
            ViewBag.RecentRegistrations = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.Conference)
                .OrderByDescending(r => r.RegistrationDate)
                .Take(5)
                .ToListAsync();

            return View();
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<bool> IsInAnyRoleAsync(
            AppUser user,
            IEnumerable<string> roleNames)
        {
            foreach (var roleName in roleNames)
            {
                if (await _userManager.IsInRoleAsync(user, roleName))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> IsReviewerRoleUserAsync(AppUser user)
        {
            return await IsInAnyRoleAsync(user, ReviewerRoleNames);
        }

        private async Task<bool> IsAuthorRoleUserAsync(AppUser user)
        {
            return await IsInAnyRoleAsync(user, AuthorRoleNames);
        }

        private async Task<bool> IsListenerRoleUserAsync(AppUser user)
        {
            return await IsInAnyRoleAsync(user, ListenerRoleNames);
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

            return Guid.TryParse(conferenceIdText, out var parsedId) && parsedId != Guid.Empty
                ? parsedId
                : null;
        }

        private static string GetCanonicalSlug(Conference conference, string? fallbackSlug = null)
        {
            return conference.Tenant?.Slug
                   ?? conference.Slug
                   ?? fallbackSlug
                   ?? "";
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

        private void SaveSelectedConference(Conference conference, string? fallbackSlug = null)
        {
            var selectedSlug = GetCanonicalSlug(conference, fallbackSlug);

            SaveSelectedConference(
                conference.TenantId,
                conference.Id,
                selectedSlug,
                conference.Title);
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

        private IQueryable<Guid> GetReviewerConferenceIds(string userId)
        {
            var reviewIds =
                from reviewAssignment in _context.ReviewAssignments.AsNoTracking()
                join submission in _context.Submissions.AsNoTracking()
                    on reviewAssignment.SubmissionId equals submission.Id
                where reviewAssignment.ReviewerId == userId
                select submission.ConferenceId;

            return reviewIds;
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

            var reviewIds = GetReviewerConferenceIds(userId);

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

        private async Task<bool> UserCanAccessConferenceAsync(
            AppUser user,
            Guid conferenceId)
        {
            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");

            if (isSuperAdmin)
            {
                return false;
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
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

            Dictionary<string, string?> dict = parsedQuery.ToDictionary(
                x => x.Key,
                x => (string?)x.Value.ToString());

            dict["conferenceId"] = conferenceId.ToString();

            return QueryHelpers.AddQueryString(newPath, dict);
        }

        private static string NormalizeProceedingBookFileUrl(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "#";
            }

            if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return filePath;
            }

            if (filePath.StartsWith("/"))
            {
                return filePath;
            }

            return "/" + filePath.TrimStart('/');
        }

        private async Task<List<Guid>> GetRegistrationOpenConferenceIdsAsync()
        {
            var today = DateTime.UtcNow.Date;

            return await _context.RegistrationTypes
                .AsNoTracking()
                .Where(rt =>
                    rt.IsActive &&
                    (
                        !rt.Deadline.HasValue ||
                        rt.Deadline.Value.Date >= today
                    ))
                .Select(rt => rt.ConferenceId)
                .Distinct()
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> ChangeConference()
        {
            var user = await _userManager.GetUserAsync(User);

            ClearSelectedConference();

            if (user != null)
            {
                var isReviewer = await IsReviewerRoleUserAsync(user);
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (isReviewer && !isAdmin)
                {
                    return RedirectToAction(nameof(Index));
                }
            }

            return RedirectToAction(nameof(MyConferences));
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
        public async Task<IActionResult> SelectConferencePost(
            Guid conferenceId,
            string? returnUrl = null)
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

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isAuthor = await IsAuthorRoleUserAsync(user);
            var isListener = await IsListenerRoleUserAsync(user);
            var isReviewer = await IsReviewerRoleUserAsync(user);

            if (isSuperAdmin)
            {
                ClearSelectedConference();

                return RedirectToAction(nameof(SuperAdmin));
            }

            if (isReviewer && !isAdmin)
            {
                ClearSelectedConference();

                return RedirectToAction(nameof(Index));
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conference == null)
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

            var selectedSlug = GetCanonicalSlug(conference, GetSlug());

            SaveSelectedConference(conference, selectedSlug);

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
                if (isAdmin)
                {
                    return Redirect($"/{selectedSlug}/Dashboard");
                }

                if (isAuthor)
                {
                    return Redirect($"/{selectedSlug}/my-submissions");
                }

                if (isListener)
                {
                    return Redirect($"/{selectedSlug}/listener-panel");
                }

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

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isAuthor = await IsAuthorRoleUserAsync(user);
            var isListener = await IsListenerRoleUserAsync(user);
            var isReviewer = await IsReviewerRoleUserAsync(user);

            if (isSuperAdmin)
            {
                ClearSelectedConference();

                return RedirectToAction(nameof(SuperAdmin));
            }

            if (!isAdmin && !isReviewer && (isAuthor || isListener))
            {
                ClearSelectedConference();

                return RedirectToAction(nameof(MyConferences));
            }

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

                    if (!isReviewer)
                    {
                        TempData["ErrorMessage"] = T(
                            "UnauthorizedPreviousConferenceAccess",
                            "Önceki seçili kongreye erişim yetkiniz yok veya kongre geçerli kurumla eşleşmiyor.");

                        return RedirectToAction(nameof(MyConferences));
                    }

                    selectedConferenceId = null;
                }
                else
                {
                    SaveSelectedConference(selectedConference, slug);
                }
            }

            if (!selectedConferenceId.HasValue)
            {
                if (isAdmin)
                {
                    if (!user.TenantId.HasValue)
                    {
                        TempData["ErrorMessage"] = T(
                            "NoInstitutionAssigned",
                            "Admin hesabınıza bağlı kurum bulunamadı.");

                        return RedirectToAction(nameof(MyConferences));
                    }

                    var autoConference = await _context.Conferences
                        .AsNoTracking()
                        .Include(c => c.Tenant)
                        .Where(c => c.TenantId == user.TenantId.Value)
                        .OrderByDescending(c => c.StartDate)
                        .FirstOrDefaultAsync();

                    if (autoConference != null)
                    {
                        var selectedSlug = GetCanonicalSlug(autoConference, slug);

                        SaveSelectedConference(autoConference, selectedSlug);

                        return Redirect($"/{selectedSlug}/Dashboard");
                    }
                }
                else if (!isReviewer)
                {
                    return RedirectToAction(nameof(MyConferences));
                }
            }

            var submissionsQuery = _context.Submissions
                .AsQueryable();

            if (isAdmin && selectedConferenceId.HasValue)
            {
                submissionsQuery = submissionsQuery
                    .Where(s => s.ConferenceId == selectedConferenceId.Value);
            }
            else if (selectedConferenceId.HasValue)
            {
                submissionsQuery = submissionsQuery
                    .Where(s =>
                        s.AuthorId == user.Id &&
                        s.ConferenceId == selectedConferenceId.Value);
            }
            else
            {
                submissionsQuery = submissionsQuery
                    .Where(s => s.AuthorId == user.Id);
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

            var hasReviewAssignment = await reviewAssignmentsQuery.AnyAsync();

            ViewBag.IsReferee = isReviewer || hasReviewAssignment;
            ViewBag.PendingReviews = pendingReviews;
            ViewBag.CompletedReviews = completedReviews;

            ViewBag.IsAuthor =
                isAuthor ||
                await submissionsQuery.AnyAsync();

            var myConferences = isAdmin && user.TenantId.HasValue
                ? await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Where(c => c.TenantId == user.TenantId.Value)
                    .OrderByDescending(c => c.StartDate)
                    .ToListAsync()
                : await GetUserConferencesAsync(user.Id);

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
                MyConferences = myConferences,
                SelectedConference = selectedConference
            };

            // Admin özgü istatistikler — sadece konferans seçiliyse
            if (isAdmin && selectedConferenceId.HasValue)
            {
                var confId = selectedConferenceId.Value;

                var regQuery = _context.Registrations.AsNoTracking()
                    .Where(r => r.ConferenceId == confId);

                viewModel.TotalRegistrations = await regQuery.CountAsync();
                viewModel.PendingPayments = await regQuery.CountAsync(r => !r.IsPaid && r.ReceiptFilePath == null);
                viewModel.ReceiptWaiting = await regQuery.CountAsync(r => !r.IsPaid && r.ReceiptFilePath != null);
                viewModel.TotalRevenue = await regQuery.Where(r => r.IsPaid).SumAsync(r => r.Amount);
                viewModel.RevenueCurrency = await _context.RegistrationTypes.AsNoTracking()
                    .Where(rt => rt.ConferenceId == confId)
                    .Select(rt => rt.Currency)
                    .FirstOrDefaultAsync() ?? "TRY";

                var trendStartDate = DateTime.UtcNow.Date.AddDays(-13);
                var trendEndDate = DateTime.UtcNow.Date.AddDays(1);

                var registrationTrendRows = await regQuery
                    .Where(r => r.RegistrationDate >= trendStartDate &&
                                r.RegistrationDate < trendEndDate)
                    .GroupBy(r => new
                    {
                        r.RegistrationDate.Year,
                        r.RegistrationDate.Month,
                        r.RegistrationDate.Day
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        g.Key.Day,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var paymentTrendRows = await regQuery
                    .Where(r => r.IsPaid &&
                                r.PaymentDate.HasValue &&
                                r.PaymentDate.Value >= trendStartDate &&
                                r.PaymentDate.Value < trendEndDate)
                    .GroupBy(r => new
                    {
                        r.PaymentDate!.Value.Year,
                        r.PaymentDate.Value.Month,
                        r.PaymentDate.Value.Day
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        g.Key.Day,
                        Amount = g.Sum(r => r.Amount)
                    })
                    .ToListAsync();

                var registrationTrendMap = registrationTrendRows
                    .ToDictionary(
                        x => new DateTime(x.Year, x.Month, x.Day),
                        x => x.Count);

                var paymentTrendMap = paymentTrendRows
                    .ToDictionary(
                        x => new DateTime(x.Year, x.Month, x.Day),
                        x => x.Amount);

                for (var day = trendStartDate; day < trendEndDate; day = day.AddDays(1))
                {
                    viewModel.TrendLabels.Add(day.ToString("dd.MM"));
                    viewModel.DailyRegistrationCounts.Add(
                        registrationTrendMap.TryGetValue(day, out var count) ? count : 0);
                    viewModel.DailyPaymentAmounts.Add(
                        paymentTrendMap.TryGetValue(day, out var amount) ? amount : 0m);
                }

                // Hakeme atanmamış kabul edilmiş / bekleyen bildirileri say
                viewModel.PendingAssignments = await _context.Submissions.AsNoTracking()
                    .CountAsync(s =>
                        s.ConferenceId == confId &&
                        (s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.New) &&
                        !_context.ReviewAssignments.Any(ra => ra.SubmissionId == s.Id));

                viewModel.TotalReferees = await _context.ReviewAssignments.AsNoTracking()
                    .Where(ra => ra.Submission != null && ra.Submission.ConferenceId == confId)
                    .Select(ra => ra.ReviewerId)
                    .Distinct()
                    .CountAsync();

                var adminUser = await _userManager.GetUserAsync(User);
                if (adminUser != null)
                {
                    viewModel.UnreadNotifications = await _context.Notifications
                        .AsNoTracking()
                        .CountAsync(n => n.UserId == adminUser.Id && !n.IsRead);
                    viewModel.TotalNotifications24h = await _context.Notifications
                        .AsNoTracking()
                        .CountAsync(n => n.UserId == adminUser.Id &&
                                        n.CreatedAt >= DateTime.UtcNow.AddHours(-24));
                }

                // ── Eksik Yapılandırma Uyarıları ────────────────────────────────
                var warnings = new List<ConfigWarning>();
                var currentSlug = GetSlug();
                var pfx = string.IsNullOrWhiteSpace(currentSlug) ? "" : $"/{currentSlug}";

                var hasRegTypes = await _context.RegistrationTypes.AsNoTracking()
                    .AnyAsync(rt => rt.ConferenceId == confId);
                if (!hasRegTypes)
                    warnings.Add(new ConfigWarning(
                        "Kayıt tipi tanımlanmamış — katılımcılar kayıt yaptıramaz.",
                        $"{pfx}/Admin/RegistrationTypes/Create",
                        "Kayıt Tipi Ekle"));

                var hasTopics = await _context.ConferenceTopics.AsNoTracking()
                    .AnyAsync(t => t.ConferenceId == confId && t.IsActive);
                if (!hasTopics)
                    warnings.Add(new ConfigWarning(
                        "Aktif konu alanı yok — bildiri gönderimleri konusuz kalır.",
                        $"{pfx}/Admin/ConferenceTopics/Create",
                        "Konu Ekle"));

                if (selectedConference != null)
                {
                    if (!selectedConference.IsSubmissionOpen)
                        warnings.Add(new ConfigWarning(
                            "Bildiri gönderimi kapalı.",
                            $"{pfx}/Admin/Conferences/Edit/{selectedConference.Id}",
                            "Kongreyi Düzenle"));

                    if (selectedConference.AbstractSubmissionDeadline.HasValue &&
                        selectedConference.AbstractSubmissionDeadline < DateTime.UtcNow)
                        warnings.Add(new ConfigWarning(
                            $"Özet gönderim son tarihi geçti ({selectedConference.AbstractSubmissionDeadline:dd.MM.yyyy}).",
                            $"{pfx}/Admin/Conferences/Edit/{selectedConference.Id}",
                            "Tarihi Güncelle"));
                }

                var stripeKey = _configuration["Stripe:SecretKey"];
                if (!HasConfiguredValue(stripeKey))
                {
                    var callerIsSuperAdmin = User.IsInRole("SuperAdmin");
                    warnings.Add(new ConfigWarning(
                        "Stripe ödeme entegrasyonu yapılandırılmamış — ödemeler çalışmaz.",
                        callerIsSuperAdmin ? "/Admin/SystemParameters" : null,
                        callerIsSuperAdmin ? "Sistem Parametreleri" : null));
                }

                viewModel.ConfigWarnings = warnings;
            }

            return View(viewModel);
        }

        [Authorize(Roles = "Author,Listener,Yazar,Dinleyici")]
        [HttpGet("/Dashboard/ProceedingBook")]
        [HttpGet("/{slug}/Dashboard/ProceedingBook")]
        public async Task<IActionResult> ProceedingBook(string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var selectedConferenceId = GetSelectedConferenceId();

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                TempData["InfoMessage"] = T(
                    "ProceedingBook_SelectConferenceFirst",
                    "Bildiri kitabını görüntülemek için önce bir kongre seçmelisiniz.");

                return RedirectToAction(nameof(MyConferences));
            }

            var conference = await GetSelectedConferenceForCurrentContextAsync(
                user,
                selectedConferenceId.Value);

            if (conference == null)
            {
                ClearSelectedConference();

                TempData["ErrorMessage"] = T(
                    "ProceedingBook_ConferenceNotFound",
                    "Bildiri kitabı görüntülenecek kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(MyConferences));
            }

            var effectiveSlug = !string.IsNullOrWhiteSpace(slug)
                ? slug
                : GetCanonicalSlug(conference, GetSlug());

            SaveSelectedConference(conference, effectiveSlug);

            var model = new ProceedingBookPageViewModel
            {
                ConferenceId = conference.Id,
                Slug = effectiveSlug,
                ConferenceTitle = conference.Title ?? "",
                ProceedingBookFilePath = conference.ProceedingBookFilePath,
                IsProceedingBookPublished = conference.IsProceedingBookPublished,
                ProceedingBookPublishedDate = conference.ProceedingBookPublishedDate,
                IsSingleConferencePage = true
            };

            if (conference.IsProceedingBookPublished &&
                !string.IsNullOrWhiteSpace(conference.ProceedingBookFilePath))
            {
                model.Books.Add(new ProceedingBookItemViewModel
                {
                    ConferenceId = conference.Id,
                    ConferenceTitle = conference.Title ?? "",
                    Slug = effectiveSlug,
                    FileUrl = NormalizeProceedingBookFileUrl(conference.ProceedingBookFilePath),
                    DownloadUrl = $"/Proceedings/Download/{conference.Id}",
                    Year = conference.StartDate.Year,
                    PublishedDate = conference.ProceedingBookPublishedDate,
                    StatusText = "Yayında",
                    CategoryText = "Bildiri Kitabı"
                });
            }

            return View("~/Views/Dashboard/ProceedingBook.cshtml", model);
        }

        [Authorize(Roles = "Listener,Dinleyici")]
        [HttpGet("/listener-panel")]
        [HttpGet("/{slug}/listener-panel")]
        public async Task<IActionResult> ListenerPanel(string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var selectedConferenceId = GetSelectedConferenceId();
            var currentSlug = slug ?? GetSlug();

            Conference? conference = null;

            if (selectedConferenceId.HasValue && selectedConferenceId.Value != Guid.Empty)
            {
                conference = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);
            }

            if (conference == null && !string.IsNullOrWhiteSpace(currentSlug))
            {
                conference = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c =>
                        c.Slug == currentSlug ||
                        (c.Tenant != null && c.Tenant.Slug == currentSlug));
            }

            if (conference == null)
            {
                return RedirectToAction(nameof(MyConferences));
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? currentSlug ?? "";

            // Kayıt bilgisi
            var registration = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == conference.Id &&
                    r.AppUserId == user.Id);

            // Katılım bilgisi
            var attendance = await _context.ConferenceAttendances
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.ConferenceId == conference.Id &&
                    a.UserId == user.Id);

            // Sertifika bilgisi
            var certificate = await _context.Certificates
                .AsNoTracking()
                .Where(c =>
                    c.ConferenceId == conference.Id &&
                    c.UserId == user.Id &&
                    c.Type == AntAbstract.Domain.Entities.CertificateType.Attendee)
                .OrderByDescending(c => c.GeneratedAt)
                .FirstOrDefaultAsync();

            // Oturum (program) sayısı
            var sessionCount = await _context.Sessions
                .AsNoTracking()
                .CountAsync(s => s.ConferenceId == conference.Id);

            ViewBag.Conference = conference;
            ViewBag.Slug = canonicalSlug;
            ViewBag.Registration = registration;
            ViewBag.Attendance = attendance;
            ViewBag.Certificate = certificate;
            ViewBag.SessionCount = sessionCount;
            ViewBag.FullName = $"{user.FirstName} {user.LastName}".Trim();

            return View();
        }

        public async Task<IActionResult> MyConferences()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isReviewer = await IsReviewerRoleUserAsync(user);

            if (isReviewer && !isAdmin)
            {
                ClearSelectedConference();

                return RedirectToAction(nameof(Index));
            }

            if (isSuperAdmin)
            {
                ClearSelectedConference();

                return RedirectToAction(nameof(SuperAdmin));
            }

            if (!isAdmin)
            {
                ClearSelectedConference();
            }

            List<Conference> registeredConferences;
            List<Conference> registrationAvailableConferences;
            List<Conference> submissionAvailableConferences;

            if (isAdmin)
            {
                if (!user.TenantId.HasValue)
                {
                    TempData["ErrorMessage"] = T(
                        "NoInstitutionAssigned",
                        "Admin hesabınıza bağlı kurum bulunamadı.");

                    registeredConferences = new List<Conference>();
                }
                else
                {
                    registeredConferences = await _context.Conferences
                        .AsNoTracking()
                        .Include(c => c.Tenant)
                        .Where(c => c.TenantId == user.TenantId.Value)
                        .OrderByDescending(c => c.StartDate)
                        .ToListAsync();
                }

                registrationAvailableConferences = new List<Conference>();
                submissionAvailableConferences = new List<Conference>();
            }
            else
            {
                var myConferenceIds = await GetUserConferenceIds(user.Id)
                    .Distinct()
                    .ToListAsync();

                var registeredConferenceIds = await _context.Registrations
                    .AsNoTracking()
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId)
                    .Distinct()
                    .ToListAsync();

                var submittedConferenceIds = await _context.Submissions
                    .AsNoTracking()
                    .Where(s => s.AuthorId == user.Id)
                    .Select(s => s.ConferenceId)
                    .Distinct()
                    .ToListAsync();

                registeredConferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Where(c => myConferenceIds.Contains(c.Id))
                    .OrderByDescending(c => c.StartDate)
                    .ToListAsync();

                var registrationOpenConferenceIds = await GetRegistrationOpenConferenceIdsAsync();

                var now = DateTime.UtcNow;

                // Yalnızca gerçekten kayıt alınabilen kongreler listelenir:
                // kayıt açık, tarihi geçmemiş ve kontenjanı dolmamış olmalı.
                registrationAvailableConferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Where(c =>
                        c.EndDate.Date >= DateTime.Today &&
                        c.IsRegistrationOpen &&
                        (
                            !c.MaxRegistrations.HasValue ||
                            c.Registrations.Count() < c.MaxRegistrations.Value
                        ) &&
                        registrationOpenConferenceIds.Contains(c.Id) &&
                        !myConferenceIds.Contains(c.Id))
                    .OrderBy(c => c.StartDate)
                    .ToListAsync();

                // Yalnızca bildiri gönderimi gerçekten açık olan kongreler listelenir.
                // Kurallar SubmissionController.EnsureUserCanCreateSubmissionAsync ile aynıdır.
                submissionAvailableConferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Where(c =>
                        c.EndDate.Date >= DateTime.Today &&
                        c.IsSubmissionOpen &&
                        (
                            !c.FullTextSubmissionDeadline.HasValue ||
                            c.FullTextSubmissionDeadline.Value >= now
                        ) &&
                        (
                            !c.AbstractSubmissionDeadline.HasValue ||
                            c.FullTextSubmissionDeadline.HasValue ||
                            c.AbstractSubmissionDeadline.Value >= now
                        ) &&
                        registeredConferenceIds.Contains(c.Id) &&
                        !submittedConferenceIds.Contains(c.Id))
                    .OrderBy(c => c.StartDate)
                    .ToListAsync();
            }

            ViewBag.AvailableConferences = registrationAvailableConferences;
            ViewBag.RegistrationAvailableConferences = registrationAvailableConferences;
            ViewBag.SubmissionAvailableConferences = submissionAvailableConferences;

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

        private static bool HasConfiguredValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            return !trimmed.StartsWith("#{", StringComparison.Ordinal) &&
                   !trimmed.StartsWith("SET_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
