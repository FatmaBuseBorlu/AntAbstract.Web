using AntAbstract.Application.DTOs.Review;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly IReviewService _reviewService;
        private readonly UserManager<AppUser> _userManager;

        public ReviewController(IReviewService reviewService, UserManager<AppUser> userManager)
        {
            _reviewService = reviewService;
            _userManager = userManager;
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
                return RedirectToAction("Index");
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
                return RedirectToAction("Evaluate", new { id = model.ReviewAssignmentId });
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                string reviewerName = $"{user.FirstName} {user.LastName}";

                await _reviewService.SubmitReviewAsync(model, reviewerName);

                TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla kaydedildi.";
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Hata: " + ex.Message;
                return RedirectToAction("Evaluate", new { id = model.ReviewAssignmentId });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin, Editor")] 
        public async Task<IActionResult> AssignReviewer(AssignReviewerDto model)
        {
            try
            {
                await _reviewService.AssignReviewerAsync(model);
                TempData["SuccessMessage"] = "Hakem başarıyla atandı.";
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Hata: " + ex.Message;
            }

            return RedirectToAction("Details", "Submission", new { id = model.SubmissionId });
        }
    }
}