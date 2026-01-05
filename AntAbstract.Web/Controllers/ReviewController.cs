using AntAbstract.Application.DTOs.Review;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly IReviewService _reviewService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly ICertificateService _certificateService;

        public ReviewController(
            IReviewService reviewService,
            UserManager<AppUser> userManager,
            AppDbContext context,
            ICertificateService certificateService)
        {
            _reviewService = reviewService;
            _userManager = userManager;
            _context = context;
            _certificateService = certificateService;
        }

        [Authorize(Roles = "Referee, Admin")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var assignments = await _reviewService.GetMyAssignmentsAsync(user.Id);
            return View(assignments);
        }

        [HttpGet]
        [Authorize(Roles = "Referee, Admin")]
        public async Task<IActionResult> Evaluate(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var assignmentDto = await _reviewService.GetAssignmentByIdAsync(id, user.Id);

            if (assignmentDto == null)
            {
                TempData["ErrorMessage"] = "Atama bulunamadı veya yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            return View(assignmentDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Referee, Admin")]
        public async Task<IActionResult> Evaluate(SubmitReviewDto model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Lütfen tüm alanları doldurunuz.";
                return RedirectToAction(nameof(Evaluate), new { id = model.ReviewAssignmentId });
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Challenge();

                var reviewerName = $"{user.FirstName} {user.LastName}".Trim();
                await _reviewService.SubmitReviewAsync(model, reviewerName);

                var conferenceId = await _context.ReviewAssignments
                    .AsNoTracking()
                    .Where(ra => ra.Id == model.ReviewAssignmentId && ra.ReviewerId == user.Id)
                    .Select(ra => ra.Submission.ConferenceId)
                    .FirstOrDefaultAsync();

                if (conferenceId != Guid.Empty)
                {
                    await _certificateService.EnsureReviewerCertificateAsync(
                        conferenceId: conferenceId,
                        reviewerUserId: user.Id,
                        reviewerFullName: reviewerName,
                        email: user.Email ?? ""
                    );
                }

                TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla kaydedildi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Hata: " + ex.Message;
                return RedirectToAction(nameof(Evaluate), new { id = model.ReviewAssignmentId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Referee, Admin")]
        public async Task<IActionResult> DeclineAssignment(int id, string Reason, string Note)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                await _reviewService.DeclineAssignmentAsync(id, user.Id, Reason, Note);
                TempData["SuccessMessage"] = "Görev iade edildi.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "İşlem başarısız: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Referee, Admin")]
        public async Task<IActionResult> DownloadCertificate(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var assignment = await _reviewService.GetAssignmentByIdAsync(id, user.Id);

            if (assignment == null || !assignment.IsReviewed)
            {
                return NotFound("Sertifika bulunamadı.");
            }

            ViewBag.ReviewerName = $"{user.Title} {user.FirstName} {user.LastName}";
            return View("Certificate", assignment);
        }

        [HttpGet]
        public IActionResult Interests() => View();

        [HttpGet]
        public IActionResult Availability() => View();

        [HttpGet]
        public IActionResult Conflicts() => View();

        [HttpGet]
        public IActionResult Guidelines() => View();

        [HttpGet]
        public IActionResult MyCertificates() => View();
    }
}
