using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

            // mevcut kodun devamı
            var conferences = await _context.Conferences
                .Include(c => c.Tenant)
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


        // URL: /Admin/Assignment/Select
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

        // URL: /{slug}/Admin/Assignment
        [HttpGet("/{slug}/Admin/Assignment")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();

            if (conferenceId == null)
            {
                TempData["ErrorMessage"] = "Lütfen bir kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == conferenceId && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bu tenant'a ait değil veya bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            var submissions = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Reviewer)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;

            return View(submissions);
        }

        // URL: /{slug}/Admin/Assignment/Assign/{id}
        // Buradaki id:guid kısıtlaması önemli.
        [HttpGet("/{slug}/Admin/Assignment/Assign/{id:guid}")]
        public async Task<IActionResult> Assign(string slug, Guid id)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            var submission = await _context.Submissions
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

        // 405 HATASINI ÇÖZEN KRİTİK METOT
        // URL: /{slug}/Admin/Assignment/Assign (POST İsteği buraya gelecek)
        [HttpPost("/{slug}/Admin/Assignment/Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPost(string slug, Guid submissionId, string reviewerId)
        {
            if (_tenantContext.Current == null) return RedirectToAction(nameof(SelectConference));

            var submission = await _context.Submissions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null || submission.Conference.TenantId != _tenantContext.Current.Id)
            {
                return NotFound("Yetkisiz erişim veya geçersiz bildiri.");
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

            await _context.SaveChangesAsync();

            // Opsiyonel: Mail gönderimi
            var reviewer = await _userManager.FindByIdAsync(reviewerId);
            if (reviewer != null)
            {
                try { await _emailService.SendEmailAsync(reviewer.Email, "Yeni Atama", "Bildiri atandı."); } catch { }
            }

            TempData["SuccessMessage"] = "Hakem ataması başarıyla tamamlandı.";
            return Redirect($"/{slug}/Admin/Assignment?conferenceId={submission.ConferenceId}");
        }
    }
}