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

namespace AntAbstract.Web.Areas.Reviewer.Controllers
{
    [Area("Reviewer")]
    [Authorize(Roles = "Referee")]
    public class ReviewController : Controller
    {
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

        [HttpGet("/Review/Index")]
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

        [HttpGet("/Review/Evaluate")]
        [HttpGet("/{slug}/Review/Evaluate")]
        public async Task<IActionResult> Evaluate(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var assignmentDto = await _reviewService.GetAssignmentByIdAsync(id, user.Id);

            if (assignmentDto == null)
            {
                TempData["ErrorMessage"] = _localizer["AssignmentNotFoundOrUnauthorized"];
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
                TempData["ErrorMessage"] = _localizer["PleaseFillAllFields"];

                return RedirectToAction(nameof(Evaluate), new
                {
                    id = model.ReviewAssignmentId
                });
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return Challenge();
                }

                var refereeName = $"{user.FirstName} {user.LastName}".Trim();

                await _reviewService.SubmitReviewAsync(model, refereeName);

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
                        refereeName,
                        user.Email ?? ""
                    );
                }

                TempData["SuccessMessage"] = _localizer["ReviewSavedSuccessfully"];

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = _localizer["ErrorPrefix"] + ex.Message;

                return RedirectToAction(nameof(Evaluate), new
                {
                    id = model.ReviewAssignmentId
                });
            }
        }

        [HttpPost("/Review/DeclineAssignment")]
        [HttpPost("/{slug}/Review/DeclineAssignment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineAssignment(int id, string Reason, string Note)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return Challenge();
                }

                await _reviewService.DeclineAssignmentAsync(id, user.Id, Reason, Note);

                TempData["SuccessMessage"] = _localizer["AssignmentReturned"];
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = _localizer["OperationFailedPrefix"] + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("/Review/DownloadCertificate/{id:int}")]
        [HttpGet("/{slug}/Review/DownloadCertificate/{id:int}")]
        public async Task<IActionResult> DownloadCertificate(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var assignment = await _reviewService.GetAssignmentByIdAsync(id, user.Id);

            if (assignment == null || !assignment.IsReviewed)
            {
                return NotFound(_localizer["CertificateNotFound"]);
            }

            ViewBag.ReviewerName = $"{user.Title} {user.FirstName} {user.LastName}";

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
            return View();
        }
    }
}