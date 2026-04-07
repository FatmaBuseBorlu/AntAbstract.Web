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
using System;
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

        public AssignmentController(
            AppDbContext context,
            TenantContext tenantContext,
            IEmailService emailService,
            UserManager<AppUser> userManager,
            IReviewerRecommendationService recommendationService,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _userManager = userManager;
            _recommendationService = recommendationService;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/Assignment")]
        public async Task<IActionResult> SelectConference()
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var conf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
                    return Redirect($"/{conf.Tenant.Slug}/Admin/Assignment?conferenceId={conf.Id}");
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
                Title = "Kongre Seç",
                Lead = "Özet ataması yapabilmek için önce kongre seçin.",
                PostUrl = "/Admin/Assignment/Select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Assignment/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId)
        {
            var conf = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
            return Redirect($"/{conf.Tenant.Slug}/Admin/Assignment?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/Assignment")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Lütfen önce geçerli bir kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();

            if (conferenceId == null)
            {
                TempData["ErrorMessage"] = "Lütfen bir kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bu kuruma ait değil veya bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            var submissions = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Reviewer)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;

            return View(submissions);
        }

        [HttpGet("/{slug}/Admin/Assignment/Assign/{id:guid}")]
        public async Task<IActionResult> Assign(string slug, Guid id)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            var submission = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null || submission.Conference.TenantId != _tenantContext.Current.Id)
            {
                TempData["ErrorMessage"] = "Bildiri bulunamadı veya erişim yetkiniz yok.";
                return Redirect($"/{slug}/Admin/Assignment");
            }

            var recommended = await _recommendationService.GetRecommendationsAsync(id);
            var allReferees = await _userManager.GetUsersInRoleAsync("Referee");

            var others = allReferees
                .Where(x => !recommended.Any(r => r.Id == x.Id))
                .ToList();

            var vm = new AssignReviewerViewModel
            {
                Submission = submission,
                RecommendedReviewers = recommended.ToList(),
                AllOtherReviewers = others
            };

            return View(vm);
        }

        [HttpPost("/{slug}/Admin/Assignment/Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPost(string slug, Guid submissionId, string reviewerId)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference));

            var submission = await _context.Submissions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null || submission.Conference.TenantId != _tenantContext.Current.Id)
            {
                return NotFound("Yetkisiz erişim veya geçersiz bildiri.");
            }

            var reviewer = await _userManager.FindByIdAsync(reviewerId);

            if (reviewer == null)
            {
                TempData["ErrorMessage"] = "Geçersiz hakem seçimi!";
                return Redirect($"/{slug}/Admin/Assignment/Assign/{submissionId}");
            }

            var alreadyAssigned = await _context.ReviewAssignments
                .AnyAsync(ra => ra.SubmissionId == submissionId && ra.ReviewerId == reviewerId);

            if (alreadyAssigned)
            {
                TempData["ErrorMessage"] = "Bu hakem zaten bu bildiriye atanmış.";
                return Redirect($"/{slug}/Admin/Assignment/Assign/{submissionId}");
            }

            _context.ReviewAssignments.Add(new ReviewAssignment
            {
                SubmissionId = submissionId,
                ReviewerId = reviewerId,
                AssignedDate = DateTime.UtcNow
            });

            if (submission.Status == SubmissionStatus.New || submission.Status == SubmissionStatus.Pending)
            {
                submission.Status = SubmissionStatus.UnderReview;
            }

            await _context.SaveChangesAsync();

            try
            {
                string mailSubject = $"Yeni Bildiri Ataması: İnceleme Bekleniyor";
                string mailBody = $"Sayın {reviewer.FirstName} {reviewer.LastName},<br><br>" +
                                  $"Kongre sistemimiz üzerinden size yeni bir bildiri değerlendirmesi atanmıştır.<br><br>" +
                                  $"<strong>Bildiri Başlığı:</strong> {submission.Title}<br><br>" +
                                  $"Lütfen en kısa sürede sisteme giriş yaparak değerlendirme formunu doldurunuz.<br><br>" +
                                  $"İyi çalışmalar dileriz.";

                await _emailService.SendAsync(reviewer.Email!, mailSubject, mailBody);
            }
            catch { }

            TempData["SuccessMessage"] = "Harika! Sistem havuzundan hakem ataması başarıyla tamamlandı ve bildirinin durumu güncellendi.";
            return Redirect($"/{slug}/Admin/Assignment?conferenceId={submission.ConferenceId}");
        }
    }
}