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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Referee")]
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

        private static string SafeExceptionMessage(
            Exception exception,
            string fallbackMessage)
        {
            if (exception == null)
            {
                return fallbackMessage;
            }

            if (string.IsNullOrWhiteSpace(exception.Message))
            {
                return fallbackMessage;
            }

            return exception.Message;
        }

        private static string BuildReviewerName(AppUser user, string fallback)
        {
            var reviewerName = $"{user.Title} {user.FirstName} {user.LastName}".Trim();

            if (string.IsNullOrWhiteSpace(reviewerName))
            {
                reviewerName = $"{user.FirstName} {user.LastName}".Trim();
            }

            if (string.IsNullOrWhiteSpace(reviewerName))
            {
                reviewerName = user.UserName ?? user.Email ?? fallback;
            }

            return reviewerName;
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

                return RedirectToAction(nameof(Evaluate), new
                {
                    id = model.ReviewAssignmentId
                });
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

            if (assignmentDto.IsReviewed)
            {
                TempData["InfoMessage"] = T(
                    "ReviewAlreadyCompleted",
                    "Bu değerlendirme daha önce tamamlanmış. Tekrar gönderim yapılamaz.");

                return RedirectToAction(nameof(Evaluate), new
                {
                    id = model.ReviewAssignmentId
                });
            }

            try
            {
                var reviewerName = BuildReviewerName(
                    user,
                    T("Reviewer", "Hakem"));

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
                        user.Email ?? string.Empty);
                }

                TempData["SuccessMessage"] = T(
                    "ReviewSavedSuccessfully",
                    "Değerlendirme başarıyla kaydedildi.");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception exception)
            {
                TempData["ErrorMessage"] = SafeExceptionMessage(
                    exception,
                    T(
                        "ReviewSaveFailed",
                        "Değerlendirme kaydedilirken bir hata oluştu."));

                return RedirectToAction(nameof(Evaluate), new
                {
                    id = model.ReviewAssignmentId
                });
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

            if (assignmentDto.IsReviewed)
            {
                TempData["ErrorMessage"] = T(
                    "CannotDeclineReviewedAssignment",
                    "Tamamlanmış değerlendirme görevi iade edilemez.");

                return RedirectToAction(nameof(Index));
            }

            try
            {
                var reason = NormalizeNullable(Reason, MaxDeclineReasonLength);
                var note = NormalizeNullable(Note, MaxDeclineNoteLength);

                await _reviewService.DeclineAssignmentAsync(
                    id,
                    user.Id,
                    reason ?? string.Empty,
                    note ?? string.Empty);

                TempData["SuccessMessage"] = T(
                    "AssignmentReturned",
                    "Değerlendirme görevi iade edildi.");
            }
            catch (Exception exception)
            {
                TempData["ErrorMessage"] = SafeExceptionMessage(
                    exception,
                    T(
                        "OperationFailed",
                        "İşlem sırasında bir hata oluştu."));
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("/Review/DownloadCertificate/{id:int}")]
        [HttpGet("/{slug}/Review/DownloadCertificate/{id:int}")]
        public async Task<IActionResult> DownloadCertificate(int id, string? slug = null)
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

            var reviewerName = BuildReviewerName(
                user,
                T("Reviewer", "Hakem"));

            var conferenceId = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra =>
                    ra.Id == id &&
                    ra.ReviewerId == user.Id)
                .Select(ra => ra.Submission.ConferenceId)
                .FirstOrDefaultAsync();

            if (conferenceId == Guid.Empty)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı."));
            }

            await _certificateService.EnsureReviewerCertificateAsync(
                conferenceId,
                user.Id,
                reviewerName,
                user.Email ?? string.Empty);

            var certificate = await _context.Certificates
                .AsNoTracking()
                .Where(c =>
                    c.ConferenceId == conferenceId &&
                    c.UserId == user.Id &&
                    c.Type == CertificateType.Reviewer)
                .OrderByDescending(c => c.GeneratedAt ?? c.EligibleAt)
                .FirstOrDefaultAsync();

            if (certificate == null)
            {
                TempData["ErrorMessage"] = T(
                    "CertificateNotReady",
                    "Hakemlik sertifikanız henüz oluşturulamadı.");

                return string.IsNullOrWhiteSpace(slug)
                    ? RedirectToAction(nameof(Index))
                    : Redirect($"/{slug}/Review/Index");
            }

            var bytes = await _certificateService.GetCertificateFileAsync(
                certificate.Id,
                user.Id);

            if (bytes == null || bytes.Length == 0)
            {
                await _certificateService.RegenerateCertificateFileAsync(certificate.Id);

                bytes = await _certificateService.GetCertificateFileAsync(
                    certificate.Id,
                    user.Id);
            }

            if (bytes == null || bytes.Length == 0)
            {
                return NotFound(T(
                    "CertificateNotFound",
                    "Sertifika bulunamadı."));
            }

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return File(
                bytes,
                certificate.ContentType ?? "application/pdf",
                certificate.FileName ?? $"reviewer_certificate_{certificate.Id}.pdf");
        }

        [HttpGet("/Review/Interests")]
        [HttpGet("/{slug}/Review/Interests")]
        public async Task<IActionResult> Interests()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            ViewBag.ExpertiseAreas = ParseCsv(user.ExpertiseAreas);

            return View();
        }

        [HttpPost("/Review/Interests")]
        [HttpPost("/{slug}/Review/Interests")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Interests(string? expertiseAreas, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            user.ExpertiseAreas = NormalizeCsv(expertiseAreas, 500);

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = T(
                    "ExpertiseSaved",
                    "Uzmanlık alanlarınız kaydedildi.");
            }
            else
            {
                TempData["ErrorMessage"] = T(
                    "ExpertiseSaveFailed",
                    "Uzmanlık alanları kaydedilirken bir hata oluştu.");
            }

            return string.IsNullOrWhiteSpace(slug)
                ? RedirectToAction(nameof(Interests))
                : Redirect($"/{slug}/Review/Interests");
        }

        [HttpGet("/Review/Availability")]
        [HttpGet("/{slug}/Review/Availability")]
        public async Task<IActionResult> Availability()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            ViewBag.UnavailableStartDate = user.ReviewerUnavailableStartDate;
            ViewBag.UnavailableEndDate = user.ReviewerUnavailableEndDate;
            ViewBag.UnavailableReason = user.ReviewerUnavailableReason;

            return View();
        }

        [HttpPost("/Review/Availability")]
        [HttpPost("/{slug}/Review/Availability")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Availability(
            DateTime? unavailableStartDate,
            DateTime? unavailableEndDate,
            string? unavailableReason,
            string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var hasAnyAvailabilityValue =
                unavailableStartDate.HasValue ||
                unavailableEndDate.HasValue ||
                !string.IsNullOrWhiteSpace(unavailableReason);

            if (!hasAnyAvailabilityValue)
            {
                user.ReviewerUnavailableStartDate = null;
                user.ReviewerUnavailableEndDate = null;
                user.ReviewerUnavailableReason = null;
            }
            else if (!unavailableStartDate.HasValue || !unavailableEndDate.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "AvailabilityDatesRequired",
                    "Müsait olmadığınız aralık için başlangıç ve bitiş tarihi seçmelisiniz.");

                return RedirectToReviewProfilePage(nameof(Availability), slug);
            }
            else if (unavailableEndDate.Value.Date < unavailableStartDate.Value.Date)
            {
                TempData["ErrorMessage"] = T(
                    "AvailabilityInvalidDateRange",
                    "Bitiş tarihi başlangıç tarihinden önce olamaz.");

                return RedirectToReviewProfilePage(nameof(Availability), slug);
            }
            else
            {
                user.ReviewerUnavailableStartDate = unavailableStartDate.Value.Date;
                user.ReviewerUnavailableEndDate = unavailableEndDate.Value.Date;
                user.ReviewerUnavailableReason = NormalizeNullable(unavailableReason, 500);
            }

            var result = await _userManager.UpdateAsync(user);

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? T("AvailabilitySaved", "Müsaitlik durumunuz kaydedildi.")
                : T("AvailabilitySaveFailed", "Müsaitlik durumu kaydedilirken bir hata oluştu.");

            return RedirectToReviewProfilePage(nameof(Availability), slug);
        }

        [HttpGet("/Review/Conflicts")]
        [HttpGet("/{slug}/Review/Conflicts")]
        public async Task<IActionResult> Conflicts()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            ViewBag.ConflictInstitutions = ParseCsv(user.ReviewerConflictInstitutions);
            ViewBag.ConflictPeople = ParseCsv(user.ReviewerConflictPeople);

            return View();
        }

        [HttpPost("/Review/Conflicts")]
        [HttpPost("/{slug}/Review/Conflicts")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Conflicts(
            string? conflictInstitutions,
            string? conflictPeople,
            string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            user.ReviewerConflictInstitutions = NormalizeCsv(conflictInstitutions, 1000);
            user.ReviewerConflictPeople = NormalizeCsv(conflictPeople, 1000);

            var result = await _userManager.UpdateAsync(user);

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
                ? T("ConflictsSaved", "Çıkar çatışması bilgileriniz kaydedildi.")
                : T("ConflictsSaveFailed", "Çıkar çatışması bilgileri kaydedilirken bir hata oluştu.");

            return RedirectToReviewProfilePage(nameof(Conflicts), slug);
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

        private static string? NormalizeCsv(string? value, int maxLength)
        {
            var normalized = string.Join(", ", ParseCsv(value));

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return normalized.Length > maxLength
                ? normalized.Substring(0, maxLength).Trim().TrimEnd(',')
                : normalized;
        }

        private IActionResult RedirectToReviewProfilePage(string actionName, string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction(actionName);
            }

            return Redirect($"/{slug}/Review/{actionName}");
        }
    }
}
