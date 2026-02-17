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

namespace AntAbstract.Web.Areas.Reviewer.Controllers
{
    [Area("Reviewer")]
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

        [HttpGet("/Review/Index")]
        [HttpGet("/{slug}/Review/Index")]
        [Authorize(Roles = "Referee, Admin")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var assignments = await _reviewService.GetMyAssignmentsAsync(user.Id);
            return View(assignments);
        }

        [HttpGet("/Review/Evaluate")]
        [HttpGet("/{slug}/Review/Evaluate")]
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

        [HttpPost("/Review/Evaluate")]
        [HttpPost("/{slug}/Review/Evaluate")]
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
                        conferenceId,
                        user.Id,
                        reviewerName,
                        user.Email ?? ""
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

        [HttpPost("/Review/DeclineAssignment")]
        [HttpPost("/{slug}/Review/DeclineAssignment")]
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

        [HttpGet("/Review/DownloadCertificate/{id:int}")]
        [HttpGet("/{slug}/Review/DownloadCertificate/{id:int}")]
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

        [HttpGet("/Review/Interests")]
        [HttpGet("/{slug}/Review/Interests")]
        public IActionResult Interests() => View();

        [HttpGet("/Review/Availability")]
        [HttpGet("/{slug}/Review/Availability")]
        public IActionResult Availability() => View();

        [HttpGet("/Review/Conflicts")]
        [HttpGet("/{slug}/Review/Conflicts")]
        public IActionResult Conflicts() => View();

        [HttpGet("/Review/Guidelines")]
        [HttpGet("/{slug}/Review/Guidelines")]
        public IActionResult Guidelines() => View();

        [HttpGet("/Review/MyCertificates")]
        [HttpGet("/{slug}/Review/MyCertificates")]
        public IActionResult MyCertificates() => View();
    }
}
