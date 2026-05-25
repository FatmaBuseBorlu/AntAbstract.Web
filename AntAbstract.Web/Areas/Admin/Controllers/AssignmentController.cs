using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Infrastructure.Services.ReviewerRecommendation;
using AntAbstract.Web.Models.ViewModels.Admin.Assignment;
using AntAbstract.Web.Models.ViewModels.Shared;
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
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AssignmentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IEmailService _emailService;
        private readonly IReviewerRecommendationService _recommendationService;
        private readonly UserManager<AppUser> _userManager;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IStringLocalizer<AssignmentController> _localizer;

        public AssignmentController(
            AppDbContext context,
            TenantContext tenantContext,
            IEmailService emailService,
            UserManager<AppUser> userManager,
            IReviewerRecommendationService recommendationService,
            ISelectedConferenceService selectedConferenceService,
            IStringLocalizer<AssignmentController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _userManager = userManager;
            _recommendationService = recommendationService;
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

        private bool IsSuperAdminUser()
        {
            return User.IsInRole("SuperAdmin");
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

            var user = await GetCurrentUserAsync();

            return user != null && user.TenantId.HasValue;
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            var user = await GetCurrentUserAsync();

            return user?.TenantId;
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

            var user = await GetCurrentUserAsync();

            if (user == null || !user.TenantId.HasValue)
            {
                return false;
            }

            return user.TenantId.Value == _tenantContext.Current.Id;
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
            Guid? conferenceId)
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

            var submissions = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Reviewer)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;

            return View(submissions);
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

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;
            ViewBag.TotalAccessibleReviewerCount = allAccessibleReferees.Count;
            ViewBag.AssignableReviewerCount = assignableReferees.Count;

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
                {
                    reviewerFullName = reviewer.UserName ?? reviewer.Email ?? T("Mail_Reviewer", "Hakem");
                }

                var mailSubject = T(
                    "Mail_NewAssignmentSubject",
                    "Yeni Hakem Ataması");

                var mailBody =
                    $"{T("Mail_Greeting", $"Sayın {reviewerFullName},")}<br><br>" +
                    $"{T("Mail_NewAssignmentIntro", "Yeni bir bildiri değerlendirme göreviniz bulunmaktadır.")}<br><br>" +
                    $"<strong>{T("Mail_SubmissionTitleLabel", "Bildiri Başlığı:")}</strong> {submission.Title}<br><br>" +
                    $"{T("Mail_FillReviewForm", "Lütfen sisteme giriş yaparak değerlendirme formunu doldurunuz.")}<br><br>" +
                    $"{T("Mail_BestRegards", "Saygılarımızla.")}";

                if (!string.IsNullOrWhiteSpace(reviewer.Email))
                {
                    await _emailService.SendAsync(
                        reviewer.Email,
                        mailSubject,
                        mailBody);
                }
            }
            catch
            {
                // Mail gönderimi başarısız olsa bile hakem ataması tamamlanmış olur.
            }

            TempData["SuccessMessage"] = T(
                "Success_ReviewerAssigned",
                "Hakem başarıyla atandı.");

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

            _context.ReviewAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_AssignmentRemoved",
                "Hakem ataması kaldırıldı.");

            return Redirect(BuildAssignmentUrl(slug, conference.Id));
        }
    }
}