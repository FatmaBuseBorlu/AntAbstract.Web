using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Infrastructure.Services.ReviewerRecommendation;
using AntAbstract.Web.Models.ViewModels.Admin.Assignment;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class AssignmentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;
        private readonly IReviewerRecommendationService _recommendationService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IStringLocalizer<AssignmentController> _localizer;
        private readonly IAuditService _audit;
        private readonly ILogger<AssignmentController> _logger;

        public AssignmentController(
            AppDbContext context,
            TenantContext tenantContext,
            IEmailService emailService,
            INotificationService notificationService,
            UserManager<AppUser> userManager,
            IAdminTenantAccessService tenantAccess,
            IReviewerRecommendationService recommendationService,
            ISelectedConferenceService selectedConferenceService,
            IStringLocalizer<AssignmentController> localizer,
            IAuditService audit,
            ILogger<AssignmentController> logger)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _notificationService = notificationService;
            _userManager = userManager;
            _tenantAccess = tenantAccess;
            _recommendationService = recommendationService;
            _logger = logger;
            _selectedConferenceService = selectedConferenceService;
            _localizer = localizer;
            _audit = audit;
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

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<bool> CurrentAdminHasTenantAsync()
        {
            if (IsSuperAdminUser())
            {
                return true;
            }

            return (await _tenantAccess.GetAdminTenantIdAsync(User)).HasValue;
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

        private async Task<Conference?> GetAccessibleConferenceAsync(
            string slug,
            Guid? conferenceId)
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

        private async Task<Submission?> GetAccessibleSubmissionAsync(
            Guid submissionId,
            Guid conferenceId,
            bool asNoTracking = true)
        {
            var query = _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Reviewer)
                .AsQueryable();

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (IsSuperAdminUser())
            {
                return await query.FirstOrDefaultAsync(s =>
                    s.Id == submissionId &&
                    s.ConferenceId == conferenceId);
            }

            if (_tenantContext.Current == null)
            {
                return null;
            }

            return await query.FirstOrDefaultAsync(s =>
                s.Id == submissionId &&
                s.ConferenceId == conferenceId &&
                s.Conference != null &&
                s.Conference.TenantId == _tenantContext.Current.Id);
        }

        private static readonly string[] ReviewerRoleNames =
        {
            "Referee",
            "Hakem",
            "Reviewer"
        };

        private const int MaxAssignmentsPerReviewer = 10;

        private async Task<List<AppUser>> GetUsersInReviewerRolesAsync()
        {
            var reviewerUsers = new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in ReviewerRoleNames)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);

                foreach (var user in usersInRole)
                {
                    if (user == null || string.IsNullOrWhiteSpace(user.Id))
                    {
                        continue;
                    }

                    reviewerUsers[user.Id] = user;
                }
            }

            return reviewerUsers.Values.ToList();
        }

        private async Task<List<AppUser>> GetAccessibleRefereesAsync(Conference conference)
        {
            var referees = await GetUsersInReviewerRolesAsync();

            if (IsSuperAdminUser())
            {
                return referees
                    .OrderBy(r => r.FirstName)
                    .ThenBy(r => r.LastName)
                    .ThenBy(r => r.Email)
                    .ToList();
            }

            var currentUser = await GetCurrentUserAsync();

            if (currentUser?.TenantId == null)
            {
                return new List<AppUser>();
            }

            return referees
                .Where(r =>
                    !r.TenantId.HasValue ||
                    r.TenantId.Value == currentUser.TenantId.Value ||
                    r.TenantId.Value == conference.TenantId)
                .OrderBy(r => r.FirstName)
                .ThenBy(r => r.LastName)
                .ThenBy(r => r.Email)
                .ToList();
        }

        private async Task<bool> CanUseReviewerForConferenceAsync(
            AppUser reviewer,
            Conference conference)
        {
            if (reviewer == null)
            {
                return false;
            }

            var reviewerHasRole = false;

            foreach (var roleName in ReviewerRoleNames)
            {
                if (await _userManager.IsInRoleAsync(reviewer, roleName))
                {
                    reviewerHasRole = true;
                    break;
                }
            }

            if (!reviewerHasRole)
            {
                return false;
            }

            if (IsSuperAdminUser())
            {
                return true;
            }

            var currentUser = await GetCurrentUserAsync();

            if (currentUser?.TenantId == null)
            {
                return false;
            }

            if (!reviewer.TenantId.HasValue)
            {
                return true;
            }

            return reviewer.TenantId.Value == currentUser.TenantId.Value ||
                   reviewer.TenantId.Value == conference.TenantId;
        }

        private static List<string> ParseCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool AnyConflictTermMatches(
            IEnumerable<string> conflictTerms,
            IEnumerable<string?> sourceValues)
        {
            var values = sourceValues
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .ToList();

            foreach (var term in conflictTerms)
            {
                foreach (var value in values)
                {
                    if (term.Length < 3 || value.Length < 3)
                    {
                        continue;
                    }

                    if (value.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        term.Contains(value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ReviewerIsUnavailable(AppUser reviewer)
        {
            if (!reviewer.ReviewerUnavailableStartDate.HasValue ||
                !reviewer.ReviewerUnavailableEndDate.HasValue)
            {
                return false;
            }

            var today = DateTime.Today;

            return reviewer.ReviewerUnavailableStartDate.Value.Date <= today &&
                   reviewer.ReviewerUnavailableEndDate.Value.Date >= today;
        }

        private static bool ReviewerHasSubmissionConflict(
            AppUser reviewer,
            Submission submission)
        {
            var conflictInstitutions = ParseCsv(reviewer.ReviewerConflictInstitutions);
            var conflictPeople = ParseCsv(reviewer.ReviewerConflictPeople);

            if (!conflictInstitutions.Any() && !conflictPeople.Any())
            {
                return false;
            }

            var institutions = new List<string?>
            {
                submission.Author?.Institution
            };

            institutions.AddRange(submission.SubmissionAuthors
                .Select(author => author.Institution));

            if (AnyConflictTermMatches(conflictInstitutions, institutions))
            {
                return true;
            }

            var people = new List<string?>
            {
                $"{submission.Author?.FirstName} {submission.Author?.LastName}".Trim(),
                submission.Author?.Email
            };

            people.AddRange(submission.SubmissionAuthors.Select(author =>
                $"{author.FirstName} {author.LastName}".Trim()));

            people.AddRange(submission.SubmissionAuthors.Select(author => author.Email));

            return AnyConflictTermMatches(conflictPeople, people);
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? _tenantContext.Current?.Slug ?? "";

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
        }

        private string BuildAssignmentUrl(string slug, Guid conferenceId)
        {
            return $"/{slug}/Admin/Assignment?conferenceId={conferenceId}";
        }

        private string BuildAssignUrl(string slug, Guid submissionId, Guid conferenceId)
        {
            return $"/{slug}/Admin/Assignment/Assign/{submissionId}?conferenceId={conferenceId}";
        }

        [HttpGet("/Admin/Assignment")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            if (!await CurrentAdminHasTenantAsync())
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

                if (!string.IsNullOrWhiteSpace(selectedConference?.Tenant?.Slug))
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect(BuildAssignmentUrl(
                        selectedConference.Tenant.Slug,
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
                    ? "Sistemde görüntülenebilecek kongre bulunamadı."
                    : "Kurumunuza bağlı görüntülenebilecek kongre bulunamadı.";
            }

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = IsSuperAdminUser()
                    ? "SuperAdmin olarak sistemdeki tüm kongreleri görebilirsiniz. Hakem ataması yapmak istediğiniz kongreyi seçiniz."
                    : T("SelectConference_Lead", "Hakem ataması yapmak için önce kongre seçiniz."),
                PostUrl = "/Admin/Assignment/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Assignment/Select")]
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

            return Redirect(BuildAssignmentUrl(
                conference.Tenant.Slug,
                conference.Id));
        }

        [HttpGet("/{slug}/Admin/Assignment")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId,
            string? search = null,
            string? status = null,
            string? assigned = null,
            int page = 1)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            // Conference-wide stats
            var allIds = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Select(s => s.Id)
                .ToListAsync();

            var totalCount = allIds.Count;

            var assignedIds = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => allIds.Contains(ra.SubmissionId))
                .Select(ra => ra.SubmissionId)
                .Distinct()
                .ToListAsync();

            var assignedCount = assignedIds.Count;

            var underReviewCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s =>
                    s.ConferenceId == conference.Id &&
                    s.Status == SubmissionStatus.UnderReview);

            // Filtered query
            var query = _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == conference.Id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var t = search.Trim();
                query = query.Where(s =>
                    (s.Title != null && s.Title.Contains(t)) ||
                    (s.Author != null && (
                        (s.Author.FirstName != null && s.Author.FirstName.Contains(t)) ||
                        (s.Author.LastName != null && s.Author.LastName.Contains(t)) ||
                        (s.Author.Email != null && s.Author.Email.Contains(t))
                    ))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<SubmissionStatus>(status, out var parsedStatus))
            {
                query = query.Where(s => s.Status == parsedStatus);
            }

            var assignedSet = assignedIds.ToHashSet();

            if (assigned == "unassigned")
            {
                query = query.Where(s => !assignedIds.Contains(s.Id));
            }
            else if (assigned == "assigned")
            {
                query = query.Where(s => assignedIds.Contains(s.Id));
            }

            const int pageSize = 30;
            if (page < 1) page = 1;

            var orderedQuery = query.OrderByDescending(s => s.CreatedDate);
            var filteredCount = await orderedQuery.CountAsync();

            var pageIds = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => s.Id)
                .ToListAsync();

            var reviewerCountsMap = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => pageIds.Contains(ra.SubmissionId))
                .GroupBy(ra => ra.SubmissionId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var allAccessibleReferees = await GetAccessibleRefereesAsync(conference);
            var accessibleReviewerIds = allAccessibleReferees.Select(r => r.Id).ToList();

            var reviewerLoads = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => accessibleReviewerIds.Contains(ra.ReviewerId) &&
                             ra.Submission != null &&
                             ra.Submission.ConferenceId == conference.Id)
                .GroupBy(ra => ra.ReviewerId)
                .Select(g => new { ReviewerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReviewerId, x => x.Count);

            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new AssignmentRowModel
                {
                    Id = s.Id,
                    Title = s.Title ?? "",
                    AuthorName = s.Author == null
                        ? ""
                        : ((s.Author.FirstName ?? "") + " " + (s.Author.LastName ?? "")).Trim(),
                    Status = s.Status.ToString(),
                    Topic = s.Topic ?? "",
                    CreatedAt = s.CreatedDate
                })
                .ToListAsync();

            foreach (var item in items)
            {
                if (reviewerCountsMap.TryGetValue(item.Id, out var c))
                    item.ReviewerCount = c;
            }

            var vm = new AssignmentIndexViewModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                Search = search,
                Status = status,
                AssignedFilter = assigned,
                TotalCount = totalCount,
                AssignedCount = assignedCount,
                UnderReviewCount = underReviewCount,
                Items = items,
                BulkReviewers = allAccessibleReferees
                    .Where(r =>
                        !ReviewerIsUnavailable(r) &&
                        (!reviewerLoads.TryGetValue(r.Id, out var load) || load < MaxAssignmentsPerReviewer))
                    .Select(r =>
                    {
                        reviewerLoads.TryGetValue(r.Id, out var load);
                        var fullName = $"{r.FirstName} {r.LastName}".Trim();

                        return new BulkReviewerOptionModel
                        {
                            Id = r.Id,
                            FullName = string.IsNullOrWhiteSpace(fullName)
                                ? r.UserName ?? r.Email ?? "Hakem"
                                : fullName,
                            Email = r.Email ?? "",
                            CurrentLoad = load,
                            MaxLoad = MaxAssignmentsPerReviewer
                        };
                    })
                    .OrderBy(r => r.FullName)
                    .ThenBy(r => r.Email)
                    .ToList(),
                Page = page,
                PageSize = pageSize,
                FilteredCount = filteredCount
            };

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;

            return View(vm);
        }

        [HttpGet("/{slug}/Admin/Assignment/Assign/{id:guid}")]
        public async Task<IActionResult> Assign(
            string slug,
            Guid id,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceBeforeProceed",
                    "İşleme devam etmek için yetkili olduğunuz bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var submission = await GetAccessibleSubmissionAsync(
                id,
                conference.Id);

            if (submission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionNotFoundOrUnauthorized",
                    "Bildiri bulunamadı veya bu bildiriye erişim yetkiniz yok.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            var allAccessibleReferees = await GetAccessibleRefereesAsync(conference);

            var assignableReferees = allAccessibleReferees
                .Where(reviewer =>
                    !ReviewerIsUnavailable(reviewer) &&
                    !ReviewerHasSubmissionConflict(reviewer, submission))
                .ToList();

            var assignableReviewerIds = assignableReferees
                .Select(r => r.Id)
                .ToHashSet();

            var recommended = await _recommendationService.GetRecommendationsAsync(id);

            var recommendedList = recommended
                .Where(r => assignableReviewerIds.Contains(r.Id))
                .ToList();

            var others = assignableReferees
                .Where(x => !recommendedList.Any(r => r.Id == x.Id))
                .ToList();

            var vm = new AssignReviewerViewModel
            {
                Submission = submission,
                RecommendedReviewers = recommendedList,
                AllOtherReviewers = others
            };

            // Her hakemin bu kongredeki mevcut görev sayısını hesapla
            var allReviewerIds = allAccessibleReferees.Select(r => r.Id).ToList();
            var reviewerLoads = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => allReviewerIds.Contains(ra.ReviewerId) &&
                             ra.Submission != null &&
                             ra.Submission.ConferenceId == conference.Id)
                .GroupBy(ra => ra.ReviewerId)
                .Select(g => new { ReviewerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReviewerId, x => x.Count);

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;
            ViewBag.TotalAccessibleReviewerCount = allAccessibleReferees.Count;
            ViewBag.AssignableReviewerCount = assignableReferees.Count;
            ViewBag.ReviewerLoads = reviewerLoads;
            ViewBag.MaxAssignmentsPerReviewer = 10;

            return View(vm);
        }

        [HttpPost("/{slug}/Admin/Assignment/Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPost(
            string slug,
            Guid submissionId,
            string reviewerId,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var submission = await GetAccessibleSubmissionAsync(
                submissionId,
                conference.Id,
                asNoTracking: false);

            if (submission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidSubmissionOrUnauthorized",
                    "Geçersiz bildiri veya bu bildiriye erişim yetkiniz yok.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            if (string.IsNullOrWhiteSpace(reviewerId))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidReviewerSelection",
                    "Geçersiz hakem seçimi.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            var reviewer = await _userManager.FindByIdAsync(reviewerId);

            if (reviewer == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidReviewerSelection",
                    "Geçersiz hakem seçimi.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            var canUseReviewer = await CanUseReviewerForConferenceAsync(
                reviewer,
                conference);

            if (!canUseReviewer)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerUnauthorized",
                    "Bu hakemi bu kongreye atama yetkiniz yok.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            if (ReviewerIsUnavailable(reviewer))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerUnavailable",
                    "Bu hakem seçtiği tarih aralığında müsait değil.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            if (ReviewerHasSubmissionConflict(reviewer, submission))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerHasConflict",
                    "Bu hakemin bildiri yazarları veya kurumlarıyla çıkar çatışması kaydı bulunuyor.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            if (submission.AuthorId == reviewer.Id)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AuthorCannotReviewOwnSubmission",
                    "Yazar kendi bildirisinin hakemi olarak atanamaz.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            var alreadyAssigned = await _context.ReviewAssignments
                .AnyAsync(ra =>
                    ra.SubmissionId == submissionId &&
                    ra.ReviewerId == reviewerId);

            if (alreadyAssigned)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerAlreadyAssigned",
                    "Bu hakem zaten bu bildiriye atanmış.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            // Hakem yük dengesi: aynı kongrede max 10 bildiri
            var currentLoad = await _context.ReviewAssignments
                .AsNoTracking()
                .CountAsync(ra =>
                    ra.ReviewerId == reviewerId &&
                    ra.Submission != null &&
                    ra.Submission.ConferenceId == conference.Id);

            if (currentLoad >= MaxAssignmentsPerReviewer)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerOverloaded",
                    $"Bu hakem bu kongre için zaten {MaxAssignmentsPerReviewer} bildiri değerlendirmesine atanmış. Farklı bir hakem seçiniz.");

                return Redirect(BuildAssignUrl(slug, submissionId, conference.Id));
            }

            _context.ReviewAssignments.Add(new ReviewAssignment
            {
                SubmissionId = submissionId,
                ReviewerId = reviewerId,
                AssignedDate = DateTime.UtcNow
            });

            if (submission.Status == SubmissionStatus.New ||
                submission.Status == SubmissionStatus.Pending)
            {
                submission.Status = SubmissionStatus.UnderReview;
            }

            await _context.SaveChangesAsync();

            try
            {
                var reviewerFullName = $"{reviewer.FirstName} {reviewer.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(reviewerFullName))
                    reviewerFullName = reviewer.UserName ?? reviewer.Email ?? "Hakem";

                var conferenceTitle = conference.Title ?? "";
                var submissionTitle = submission.Title ?? "";
                var reviewUrl = $"/{slug}/Review/Index";

                // ── İn-app bildirim (Hakem) ────────────────────────────────────
                await _notificationService.CreateAsync(
                    userId: reviewer.Id,
                    title: "Yeni Değerlendirme Görevi",
                    message: $"\"{submissionTitle}\" bildirisi değerlendirmeniz için atandı.",
                    icon: "📋",
                    color: "primary",
                    link: reviewUrl);

                // ── İn-app bildirim (Yazar — bildirisinin incelemeye alındığı) ─
                if (!string.IsNullOrWhiteSpace(submission.AuthorId))
                {
                    await _notificationService.CreateAsync(
                        userId: submission.AuthorId,
                        title: "Bildiriniz İncelemede",
                        message: $"\"{submissionTitle}\" başlıklı bildiriniz hakem incelemesine alındı.",
                        icon: "🔍",
                        color: "info",
                        link: null);
                }

                // ── Hakem e-postası (güzel HTML) ───────────────────────────────
                if (!string.IsNullOrWhiteSpace(reviewer.Email))
                {
                    var htmlBody = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:#1a2d5a;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>📋 Yeni Değerlendirme Görevi</h2>
    <p style='margin:6px 0 0;opacity:.85;font-size:14px'>{System.Net.WebUtility.HtmlEncode(conferenceTitle)}</p>
  </div>
  <div style='background:#f9fafb;padding:24px 32px'>
    <p>Sayın <strong>{System.Net.WebUtility.HtmlEncode(reviewerFullName)}</strong>,</p>
    <p>Aşağıdaki bildiri sizin değerlendirmeniz için atanmıştır:</p>
    <div style='background:#fff;border-left:4px solid #1a2d5a;padding:12px 16px;margin:16px 0;border-radius:4px'>
      <p style='margin:0;font-weight:600;color:#1a2d5a'>{System.Net.WebUtility.HtmlEncode(submissionTitle)}</p>
    </div>
    <p>Lütfen sisteme giriş yaparak değerlendirme formunu doldurunuz.</p>
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>";

                    await _emailService.SendAsync(
                        reviewer.Email,
                        $"Yeni Değerlendirme Görevi — {conferenceTitle}",
                        htmlBody);
                }
            }
            catch
            {
                // Bildirim/mail hatası atama işlemini durdurmaz.
            }

            TempData["SuccessMessage"] = T(
                "Success_ReviewerAssigned",
                "Hakem başarıyla atandı.");

            var adminUser = await _userManager.GetUserAsync(User);
            await _audit.LogAsync(
                category: "Review",
                action: "ReviewerAssigned",
                userId: adminUser?.Id,
                userName: adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                entityType: "Submission",
                entityId: submissionId.ToString(),
                description: $"Hakem atandı: {reviewer.Email} → '{submission.Title}'",
                conferenceId: conference.Id,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Redirect(BuildAssignmentUrl(slug, conference.Id));
        }

        [HttpPost("/{slug}/Admin/Assignment/BulkAssign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAssign(
            string slug,
            List<Guid>? submissionIds,
            string reviewerId,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var selectedIds = (submissionIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (!selectedIds.Any())
            {
                TempData["ErrorMessage"] = T(
                    "Error_NoBulkAssignmentSelection",
                    "Lütfen hakem atanacak en az bir bildiri seçin.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            if (string.IsNullOrWhiteSpace(reviewerId))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidReviewerSelection",
                    "Geçersiz hakem seçimi.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            var reviewer = await _userManager.FindByIdAsync(reviewerId);

            if (reviewer == null ||
                !await CanUseReviewerForConferenceAsync(reviewer, conference))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerUnauthorized",
                    "Bu hakemi bu kongreye atama yetkiniz yok.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            if (ReviewerIsUnavailable(reviewer))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerUnavailable",
                    "Bu hakem seçtiği tarih aralığında müsait değil.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            var currentLoad = await _context.ReviewAssignments
                .AsNoTracking()
                .CountAsync(ra =>
                    ra.ReviewerId == reviewerId &&
                    ra.Submission != null &&
                    ra.Submission.ConferenceId == conference.Id);

            if (currentLoad >= MaxAssignmentsPerReviewer)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ReviewerOverloaded",
                    $"Bu hakem bu kongre için zaten {MaxAssignmentsPerReviewer} bildiri değerlendirmesine atanmış. Farklı bir hakem seçiniz.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            var submissions = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.ReviewAssignments)
                .Where(s =>
                    selectedIds.Contains(s.Id) &&
                    s.ConferenceId == conference.Id)
                .ToListAsync();

            var skippedNotFound = selectedIds.Count - submissions.Count;
            var skippedFinalStatus = 0;
            var skippedAlreadyAssigned = 0;
            var skippedConflict = 0;
            var skippedOwnSubmission = 0;
            var skippedLoadLimit = 0;
            var assignedSubmissions = new List<Submission>();
            var now = DateTime.UtcNow;

            foreach (var submission in submissions)
            {
                if (submission.Status == SubmissionStatus.Accepted ||
                    submission.Status == SubmissionStatus.Rejected ||
                    submission.Status == SubmissionStatus.Presented)
                {
                    skippedFinalStatus++;
                    continue;
                }

                if (submission.ReviewAssignments.Any(ra => ra.ReviewerId == reviewerId))
                {
                    skippedAlreadyAssigned++;
                    continue;
                }

                if (submission.AuthorId == reviewer.Id)
                {
                    skippedOwnSubmission++;
                    continue;
                }

                if (ReviewerHasSubmissionConflict(reviewer, submission))
                {
                    skippedConflict++;
                    continue;
                }

                if (currentLoad + assignedSubmissions.Count >= MaxAssignmentsPerReviewer)
                {
                    skippedLoadLimit++;
                    continue;
                }

                _context.ReviewAssignments.Add(new ReviewAssignment
                {
                    SubmissionId = submission.Id,
                    ReviewerId = reviewerId,
                    AssignedDate = now
                });

                if (submission.Status == SubmissionStatus.New ||
                    submission.Status == SubmissionStatus.Pending)
                {
                    submission.Status = SubmissionStatus.UnderReview;
                }

                assignedSubmissions.Add(submission);
            }

            if (!assignedSubmissions.Any())
            {
                TempData["ErrorMessage"] = T(
                    "Error_NoBulkAssignmentsCreated",
                    "Seçilen bildiriler için yeni hakem ataması yapılamadı. Atanmış, sonuçlanmış, çıkar çatışması olan veya yük limiti dolmuş kayıtlar olabilir.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            await _context.SaveChangesAsync();

            try
            {
                var reviewerFullName = $"{reviewer.FirstName} {reviewer.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(reviewerFullName))
                    reviewerFullName = reviewer.UserName ?? reviewer.Email ?? "Hakem";

                var reviewUrl = $"/{slug}/Review/Index";
                var conferenceTitle = conference.Title ?? "";

                await _notificationService.CreateAsync(
                    userId: reviewer.Id,
                    title: "Yeni Değerlendirme Görevleri",
                    message: $"{assignedSubmissions.Count} bildiri değerlendirmeniz için atandı.",
                    icon: "📋",
                    color: "primary",
                    link: reviewUrl);

                foreach (var submission in assignedSubmissions)
                {
                    if (string.IsNullOrWhiteSpace(submission.AuthorId))
                    {
                        continue;
                    }

                    await _notificationService.CreateAsync(
                        userId: submission.AuthorId,
                        title: "Bildiriniz İncelemede",
                        message: $"\"{submission.Title}\" başlıklı bildiriniz hakem incelemesine alındı.",
                        icon: "🔍",
                        color: "info",
                        link: null);
                }

                if (!string.IsNullOrWhiteSpace(reviewer.Email))
                {
                    var titles = assignedSubmissions
                        .Take(10)
                        .Select(s => $"<li>{System.Net.WebUtility.HtmlEncode(s.Title ?? "Başlıksız bildiri")}</li>");

                    var extraCount = assignedSubmissions.Count > 10
                        ? $"<p>+ {assignedSubmissions.Count - 10} bildiri daha</p>"
                        : "";

                    var htmlBody = $@"
<div style='font-family:Arial,sans-serif;max-width:640px;margin:auto'>
  <div style='background:#1a2d5a;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>📋 Yeni Değerlendirme Görevleri</h2>
    <p style='margin:6px 0 0;opacity:.85;font-size:14px'>{System.Net.WebUtility.HtmlEncode(conferenceTitle)}</p>
  </div>
  <div style='background:#f9fafb;padding:24px 32px'>
    <p>Sayın <strong>{System.Net.WebUtility.HtmlEncode(reviewerFullName)}</strong>,</p>
    <p>{assignedSubmissions.Count} bildiri sizin değerlendirmeniz için atanmıştır:</p>
    <ul style='background:#fff;border-left:4px solid #1a2d5a;padding:16px 16px 16px 32px;margin:16px 0;border-radius:4px'>
      {string.Join("", titles)}
    </ul>
    {extraCount}
    <p>Lütfen sisteme giriş yaparak değerlendirme formlarını doldurunuz.</p>
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>";

                    await _emailService.SendAsync(
                        reviewer.Email,
                        $"Yeni Değerlendirme Görevleri — {conferenceTitle}",
                        htmlBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Toplu hakem atama bildirimi gönderilemedi.");
            }

            var skippedTotal = skippedNotFound +
                               skippedFinalStatus +
                               skippedAlreadyAssigned +
                               skippedConflict +
                               skippedOwnSubmission +
                               skippedLoadLimit;

            var successMessage = $"{assignedSubmissions.Count} hakem ataması oluşturuldu.";
            if (skippedTotal > 0)
            {
                successMessage += $" {skippedTotal} seçim atlandı.";
            }

            TempData["SuccessMessage"] = successMessage;

            var adminUser = await _userManager.GetUserAsync(User);
            await _audit.LogAsync(
                category: "Review",
                action: "BulkReviewerAssigned",
                userId: adminUser?.Id,
                userName: adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                entityType: "ReviewAssignment",
                entityId: reviewerId,
                description: $"Toplu hakem atama: {reviewer.Email} için {assignedSubmissions.Count} atama eklendi, {skippedTotal} seçim atlandı.",
                conferenceId: conference.Id,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Redirect(BuildAssignmentUrl(slug, conference.Id));
        }

        [HttpPost("/{slug}/Admin/Assignment/Remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAssignment(
            string slug,
            int assignmentId,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var assignment = await _context.ReviewAssignments
                .Include(ra => ra.Submission)
                .Include(ra => ra.Review)
                .FirstOrDefaultAsync(ra =>
                    ra.Id == assignmentId &&
                    ra.Submission != null &&
                    ra.Submission.ConferenceId == conference.Id);

            if (assignment == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AssignmentNotFound",
                    "Hakem ataması bulunamadı veya bu atamaya erişim yetkiniz yok.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            if (assignment.Review != null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AssignmentHasReview",
                    "Değerlendirmesi yapılmış hakem ataması kaldırılamaz.");

                return Redirect(BuildAssignmentUrl(slug, conference.Id));
            }

            var reviewerId = assignment.ReviewerId;
            var submissionTitle = assignment.Submission?.Title ?? "";

            _context.ReviewAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            // Hakeme in-app bildirim
            try
            {
                if (!string.IsNullOrWhiteSpace(reviewerId))
                {
                    await _notificationService.CreateAsync(
                        userId: reviewerId,
                        title: "Değerlendirme Görevi İptal Edildi",
                        message: $"\"{submissionTitle}\" bildirisi için atama yönetici tarafından kaldırıldı.",
                        icon: "❌",
                        color: "warning",
                        link: null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Atama iptal bildirimi gönderilemedi.");
            }

            TempData["SuccessMessage"] = T(
                "Success_AssignmentRemoved",
                "Hakem ataması kaldırıldı.");

            var adminUser2 = await _userManager.GetUserAsync(User);
            await _audit.LogAsync(
                category: "Review",
                action: "ReviewerRemoved",
                userId: adminUser2?.Id,
                userName: adminUser2 != null ? $"{adminUser2.FirstName} {adminUser2.LastName}".Trim() : null,
                entityType: "ReviewAssignment",
                entityId: assignmentId.ToString(),
                description: $"Hakem ataması kaldırıldı: '{submissionTitle}'",
                conferenceId: conference.Id,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Redirect(BuildAssignmentUrl(slug, conference.Id));
        }

        // ── Workload Dashboard ───────────────────────────────────────────────────
        [HttpGet("/{slug}/Admin/Assignment/Workload")]
        [HttpGet("/Admin/Assignment/Workload")]
        public async Task<IActionResult> Workload(string? slug, Guid? conferenceId)
        {
            var conference = await ResolveConferenceAsync(slug, conferenceId);
            if (conference == null)
                return Redirect($"/{slug}/Admin/Assignment");

            const int MaxLoad = 10;

            var referees = await GetAccessibleRefereesAsync(conference);
            var refereeIds = referees.Select(r => r.Id).ToList();

            // Atamalar: her hakem için
            var assignments = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(a => refereeIds.Contains(a.ReviewerId)
                         && a.Submission.ConferenceId == conference.Id
                         && !a.IsDeclined)
                .Include(a => a.Review)
                .Include(a => a.Submission)
                .ToListAsync();

            // Bidler: her hakemin "İncelemek İstiyorum" sayısı
            var bids = await _context.ReviewBids
                .AsNoTracking()
                .Where(b => refereeIds.Contains(b.ReviewerId)
                         && b.Submission.ConferenceId == conference.Id)
                .GroupBy(b => b.ReviewerId)
                .Select(g => new
                {
                    ReviewerId = g.Key,
                    WantCount = g.Count(x => x.Preference == BidPreference.WantToReview),
                    ConflictCount = g.Count(x => x.Preference == BidPreference.ConflictOfInterest),
                })
                .ToListAsync();

            var bidMap = bids.ToDictionary(b => b.ReviewerId);

            var rows = referees.Select(r =>
            {
                var myAssignments = assignments.Where(a => a.ReviewerId == r.Id).ToList();
                var reviewed = myAssignments.Count(a => a.Review != null);
                var pending = myAssignments.Count(a => a.Review == null);
                bidMap.TryGetValue(r.Id, out var bid);

                return new
                {
                    r.Id,
                    FullName = $"{r.FirstName} {r.LastName}".Trim(),
                    r.Email,
                    Total = myAssignments.Count,
                    Reviewed = reviewed,
                    Pending = pending,
                    WantBids = bid?.WantCount ?? 0,
                    ConflictBids = bid?.ConflictCount ?? 0,
                    LoadPct = (int)Math.Round(myAssignments.Count * 100.0 / MaxLoad),
                };
            })
            .OrderByDescending(r => r.Total)
            .ToList();

            // Atanmamış bildiri sayısı
            var unassignedCount = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id
                         && s.Status != SubmissionStatus.Accepted
                         && s.Status != SubmissionStatus.Rejected)
                .CountAsync(s => !_context.ReviewAssignments
                    .Any(a => a.SubmissionId == s.Id && !a.IsDeclined));

            ViewBag.Slug = slug ?? "";
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.MaxLoad = MaxLoad;
            ViewBag.UnassignedCount = unassignedCount;
            ViewBag.TotalSubmissions = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s => s.ConferenceId == conference.Id);

            return View(rows.Cast<object>().ToList());
        }

        private async Task<Conference?> ResolveConferenceAsync(string? slug, Guid? conferenceId)
        {
            Guid? id = conferenceId;
            if (!id.HasValue || id.Value == Guid.Empty)
                id = _selectedConferenceService.GetSelectedConferenceId();
            if (!id.HasValue)
                return null;

            return await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id.Value);
        }
    }
}
