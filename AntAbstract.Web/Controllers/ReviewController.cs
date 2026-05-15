using AntAbstract.Application.DTOs.Review;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Referee,Admin")]
    public class ReviewController : Controller
    {
        private const int MaxDeclineReasonLength = 200;
        private const int MaxDeclineNoteLength = 1000;

        private readonly IReviewService _reviewService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly ICertificateService _certificateService;
        private readonly IStringLocalizer<ReviewController> _localizer;

        public ReviewController(
            IReviewService reviewService,
            UserManager<AppUser> userManager,
            AppDbContext context,
            ICertificateService certificateService,
            IStringLocalizer<ReviewController> localizer)
        {
            _reviewService = reviewService;
            _userManager = userManager;
            _context = context;
            _certificateService = certificateService;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private static string? NormalizeNullable(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            if (value.Length > maxLength)
            {
                value = value.Substring(0, maxLength);
            }

            return value;
        }

        [HttpGet("/Review")]
        [HttpGet("/Review/Index")]
        [HttpGet("/{slug}/Review")]
        [HttpGet("/{slug}/Review/Index")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var assignments = await _reviewService.GetMyAssignmentsAsync(user.Id);

            return View(assignments);
        }

        [HttpGet("/Review/Evaluate/{id:int}")]
        [HttpGet("/{slug}/Review/Evaluate/{id:int}")]
        public async Task<IActionResult> Evaluate(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = T(
                    "InvalidAssignment",
                    "Geçersiz değerlendirme görevi.");

                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var assignmentDto = await _reviewService.GetAssignmentByIdAsync(id, user.Id);

            if (assignmentDto == null)
            {
                TempData["ErrorMessage"] = T(
                    "AssignmentNotFoundOrUnauthorized",
                    "Değerlendirme görevi bulunamadı veya bu göreve erişim yetkiniz yok.");

                return RedirectToAction(nameof(Index));
            }

            return View(assignmentDto);
        }

        [HttpPost("/Review/Evaluate")]
        [HttpPost("/{slug}/Review/Evaluate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Evaluate(SubmitReviewDto model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = T(
                    "PleaseFillAllFields",
                    "Lütfen zorunlu alanları doldurun.");

                return RedirectToAction(nameof(Evaluate), new { id = model.ReviewAssignmentId });
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var assignmentDto = await _reviewService.GetAssignmentByIdAsync(
                model.ReviewAssignmentId,
                user.Id);

            if (assignmentDto == null)
            {
                TempData["ErrorMessage"] = T(
                    "AssignmentNotFoundOrUnauthorized",
                    "Değerlendirme görevi bulunamadı veya bu göreve erişim yetkiniz yok.");

                return RedirectToAction(nameof(Index));
            }

            try
            {
                var reviewerName = $"{user.FirstName} {user.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(reviewerName))
                {
                    reviewerName = user.UserName ?? user.Email ?? T("Reviewer", "Hakem");
                }

                await _reviewService.SubmitReviewAsync(model, reviewerName);

                var conferenceId = await _context.ReviewAssignments
                    .AsNoTracking()
                    .Where(ra =>
                        ra.Id == model.ReviewAssignmentId &&
                        ra.ReviewerId == user.Id)
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

                TempData["SuccessMessage"] = T(
                    "ReviewSavedSuccessfully",
                    "Değerlendirme başarıyla kaydedildi.");

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] = T(
                    "ReviewSaveFailed",
                    "Değerlendirme kaydedilirken bir hata oluştu.");

                return RedirectToAction(nameof(Evaluate), new { id = model.ReviewAssignmentId });
            }
        }

        [HttpPost("/Review/DeclineAssignment")]
        [HttpPost("/{slug}/Review/DeclineAssignment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineAssignment(
            int id,
            string? Reason,
            string? Note)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = T(
                    "InvalidAssignment",
                    "Geçersiz değerlendirme görevi.");

                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var assignmentDto = await _reviewService.GetAssignmentByIdAsync(id, user.Id);

            if (assignmentDto == null)
            {
                TempData["ErrorMessage"] = T(
                    "AssignmentNotFoundOrUnauthorized",
                    "Değerlendirme görevi bulunamadı veya bu göreve erişim yetkiniz yok.");

                return RedirectToAction(nameof(Index));
            }

            try
            {
                var reason = NormalizeNullable(Reason, MaxDeclineReasonLength);
                var note = NormalizeNullable(Note, MaxDeclineNoteLength);

                await _reviewService.DeclineAssignmentAsync(
                    id,
                    user.Id,
                    reason ?? "",
                    note ?? "");

                TempData["SuccessMessage"] = T(
                    "AssignmentReturned",
                    "Değerlendirme görevi iade edildi.");
            }
            catch
            {
                TempData["ErrorMessage"] = T(
                    "OperationFailed",
                    "İşlem sırasında bir hata oluştu.");
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("/Review/DownloadCertificate/{id:int}")]
        [HttpGet("/{slug}/Review/DownloadCertificate/{id:int}")]
        public async Task<IActionResult> DownloadCertificate(int id)
        {
            if (id <= 0)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı."));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var assignment = await _reviewService.GetAssignmentByIdAsync(id, user.Id);

            if (assignment == null || !assignment.IsReviewed)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı."));
            }

            ViewBag.ReviewerName = $"{user.Title} {user.FirstName} {user.LastName}".Trim();

            return View("Certificate", assignment);
        }

        [HttpGet("/Review/Interests")]
        [HttpGet("/{slug}/Review/Interests")]
        public IActionResult Interests()
        {
            return View();
        }

        [HttpGet("/Review/Availability")]
        [HttpGet("/{slug}/Review/Availability")]
        public IActionResult Availability()
        {
            return View();
        }

        [HttpGet("/Review/Conflicts")]
        [HttpGet("/{slug}/Review/Conflicts")]
        public IActionResult Conflicts()
        {
            return View();
        }

        [HttpGet("/Review/Guidelines")]
        [HttpGet("/{slug}/Review/Guidelines")]
        public IActionResult Guidelines()
        {
            return View();
        }

        [HttpGet("/Review/MyCertificates")]
        [HttpGet("/{slug}/Review/MyCertificates")]
        public IActionResult MyCertificates()
        {
            return RedirectToAction("Index", "Certificates");
        }
    }
}