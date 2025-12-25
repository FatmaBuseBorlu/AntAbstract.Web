using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Editor")]
    public class SubmissionController : Controller
    {
        private readonly ISubmissionService _submissionService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IReviewService _reviewService;

        public SubmissionController(
            ISubmissionService submissionService,
            UserManager<AppUser> userManager,
            IReviewService reviewService)
        {
            _submissionService = submissionService;
            _userManager = userManager;
            _reviewService = reviewService;
        }

        public async Task<IActionResult> Index()
        {
            var submissions = await _submissionService.GetAllSubmissionsAsync();
            return View(submissions);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var submission = await _submissionService.GetSubmissionByIdAsync(id);
            if (submission == null) return NotFound();

            ViewBag.Referees = await _userManager.GetUsersInRoleAsync("Referee");

            ViewBag.Reviews = await _reviewService.GetReviewsBySubmissionIdAsync(id);

            return View(submission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(Guid id, string status)
        {
            if (Enum.TryParse<SubmissionStatus>(status, out var newStatus))
            {
                await _submissionService.UpdateStatusAsync(id, newStatus);
                TempData["SuccessMessage"] = "Bildiri durumu güncellendi: " + status;
            }
            else
            {
                TempData["ErrorMessage"] = "Geçersiz durum bilgisi.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _submissionService.DeleteSubmissionAsync(id);
            TempData["SuccessMessage"] = "Bildiri silindi.";
            return RedirectToAction("Index");
        }
    }
}