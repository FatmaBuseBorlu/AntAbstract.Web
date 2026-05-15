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
    [Authorize(Roles = "Admin,Organizator")]
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

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        private async Task<bool> IsCurrentUserAdminAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            return user != null &&
                   await _userManager.IsInRoleAsync(user, "Admin");
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

        private async Task<List<AppUser>> GetAccessibleRefereesAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null &&
                          await _userManager.IsInRoleAsync(currentUser, "Admin");

            var referees = await _userManager.GetUsersInRoleAsync("Referee");

            if (!isAdmin)
            {
                if (currentUser?.TenantId == null)
                {
                    return new List<AppUser>();
                }

                referees = referees
                    .Where(r => r.TenantId == currentUser.TenantId.Value)
                    .ToList();
            }

            return referees.ToList();
        }

        [HttpGet("/Admin/Assignment")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId != null)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var conf = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
                    HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect($"/{conf.Tenant.Slug}/Admin/Assignment?conferenceId={conf.Id}");
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = T("SelectConference_Lead", "Hakem ataması yapmak için önce kongre seçiniz."),
                PostUrl = "/Admin/Assignment/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Assignment/Select")]
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

            return Redirect($"/{conf.Tenant.Slug}/Admin/Assignment?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/Assignment")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

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

            return View(submissions);
        }

        [HttpGet("/{slug}/Admin/Assignment/Assign/{id:guid}")]
        public async Task<IActionResult> Assign(string slug, Guid id)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
                !await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceBeforeProceed",
                    "İşleme devam etmek için yetkili olduğunuz bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            if (!await CanAccessSubmissionAsync(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionNotFoundOrUnauthorized",
                    "Bildiri bulunamadı veya bu bildiriye erişim yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Assignment");
            }

            var submission = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.Conference != null &&
                    s.Conference.TenantId == _tenantContext.Current.Id);

            if (submission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionNotFoundOrUnauthorized",
                    "Bildiri bulunamadı veya bu bildiriye erişim yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Assignment");
            }

            var accessibleReferees = await GetAccessibleRefereesAsync();

            var accessibleReviewerIds = accessibleReferees
                .Select(r => r.Id)
                .ToHashSet();

            var recommended = await _recommendationService.GetRecommendationsAsync(id);

            var recommendedList = recommended
                .Where(r => accessibleReviewerIds.Contains(r.Id))
                .ToList();

            var others = accessibleReferees
                .Where(x => !recommendedList.Any(r => r.Id == x.Id))
                .ToList();

            var vm = new AssignReviewerViewModel
            {
                Submission = submission,
                RecommendedReviewers = recommendedList,
                AllOtherReviewers = others
            };

            return View(vm);
        }

        [HttpPost("/{slug}/Admin/Assignment/Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPost(string slug, Guid submissionId, string reviewerId)
        {
            if (_tenantContext.Current == null ||
                !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
                !await CanAccessCurrentTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectValidConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            if (!await CanAccessSubmissionAsync(submissionId))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidSubmissionOrUnauthorized",
                    "Geçersiz bildiri veya bu bildiriye erişim yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Assignment");
            }

            var submission = await _context.Submissions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s =>
                    s.Id == submissionId &&
                    s.Conference != null &&
                    s.Conference.TenantId == _tenantContext.Current.Id);

            if (submission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidSubmissionOrUnauthorized",
                    "Geçersiz bildiri veya bu bildiriye erişim yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Assignment");
            }

            if (string.IsNullOrWhiteSpace(reviewerId))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidReviewerSelection",
                    "Geçersiz hakem seçimi.");

                return Redirect($"/{slug}/Admin/Assignment/Assign/{submissionId}");
            }

            var reviewer = await _userManager.FindByIdAsync(reviewerId);

            if (reviewer == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidReviewerSelection",
                    "Geçersiz hakem seçimi.");

                return Redirect($"/{slug}/Admin/Assignment/Assign/{submissionId}");
            }

            var reviewerIsReferee = await _userManager.IsInRoleAsync(reviewer, "Referee");

            if (!reviewerIsReferee)
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidReviewerSelection",
                    "Seçilen kullanıcı hakem rolüne sahip değil.");

                return Redirect($"/{slug}/Admin/Assignment/Assign/{submissionId}");
            }

            var isAdmin = await IsCurrentUserAdminAsync();

            if (!isAdmin)
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser?.TenantId == null ||
                    reviewer.TenantId != currentUser.TenantId.Value)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_ReviewerUnauthorized",
                        "Bu hakemi bu kongreye atama yetkiniz yok.");

                    return Redirect($"/{slug}/Admin/Assignment/Assign/{submissionId}");
                }
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

                return Redirect($"/{slug}/Admin/Assignment/Assign/{submissionId}");
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
                    await _emailService.SendAsync(reviewer.Email, mailSubject, mailBody);
                }
            }
            catch
            {
                // Mail gönderimi başarısız olsa bile hakem ataması tamamlanmış olur.
            }

            TempData["SuccessMessage"] = T(
                "Success_ReviewerAssigned",
                "Hakem başarıyla atandı.");

            return Redirect($"/{slug}/Admin/Assignment?conferenceId={submission.ConferenceId}");
        }
    }
}