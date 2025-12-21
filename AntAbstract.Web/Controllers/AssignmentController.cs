using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]

    // Hem slug’lı hem slug’sız giriş:
    [Route("{slug}/admin/assignment")]
    [Route("admin/assignment")]
    public class AssignmentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IEmailService _emailService;
        private readonly IReviewerRecommendationService _recommendationService;
        private readonly UserManager<AppUser> _userManager;

        public AssignmentController(
            AppDbContext context,
            TenantContext tenantContext,
            IEmailService emailService,
            UserManager<AppUser> userManager,
            IReviewerRecommendationService recommendationService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _userManager = userManager;
            _recommendationService = recommendationService;
        }

        // /admin/assignment
        // /{slug}/admin/assignment
        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen bir kongre URL'i (slug) üzerinden işlem yapın.";
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return View(new List<Submission>());
            }

            var submissions = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Reviewer)
                .ToListAsync();

            return View(submissions);
        }

        // /admin/assignment/assign/{id}
        // /{slug}/admin/assignment/assign/{id}
        [HttpGet("assign/{id:guid}")]
        public async Task<IActionResult> Assign(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null) return NotFound();

            var recommendedReviewers = await _recommendationService.GetRecommendationsAsync(id);

            // Sende rol adı "Referee" idi:
            var allReferees = await _userManager.GetUsersInRoleAsync("Referee");

            var allOtherReferees = allReferees
                .Where(x => !recommendedReviewers.Any(r => r.Id == x.Id))
                .ToList();

            var viewModel = new AssignReviewerViewModel
            {
                Submission = submission,
                RecommendedReviewers = recommendedReviewers.ToList(),
                AllOtherReviewers = allOtherReferees
            };

            return View(viewModel);
        }

        // POST: /admin/assignment/assign
        // POST: /{slug}/admin/assignment/assign
        [HttpPost("assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(Guid submissionId, string reviewerId)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null) return NotFound("Bildiri bulunamadı.");

            var reviewer = await _userManager.FindByIdAsync(reviewerId);
            if (reviewer == null)
            {
                TempData["ErrorMessage"] = "Seçilen hakem bulunamadı.";
                return RedirectToAction(nameof(Assign), new { id = submissionId });
            }

            bool alreadyAssigned = await _context.ReviewAssignments
                .AnyAsync(ra => ra.SubmissionId == submissionId && ra.ReviewerId == reviewerId);

            if (alreadyAssigned)
            {
                TempData["ErrorMessage"] = "Bu hakem zaten bu bildiriye atanmış.";
                return RedirectToAction(nameof(Assign), new { id = submissionId });
            }

            var assignment = new ReviewAssignment
            {
                SubmissionId = submissionId,
                ReviewerId = reviewerId,
                AssignedDate = DateTime.UtcNow
            };

            _context.ReviewAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendEmailAsync(
                    reviewer.Email,
                    "Yeni Bildiri Ataması",
                    $"Sayın {reviewer.UserName}, size değerlendirmeniz için yeni bir bildiri atandı."
                );
            }
            catch
            {
                // Mail patlarsa atamayı geri alma; sadece sessiz geç.
            }

            TempData["SuccessMessage"] = "Hakem ataması başarıyla tamamlandı.";
            return RedirectToAction(nameof(Index));
        }
    }
}
