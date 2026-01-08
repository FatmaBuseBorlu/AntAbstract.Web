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
            catch (System.Exception ex)
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
        public IActionResult Interests()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Availability()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Conflicts()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Guidelines()
        {
            return View();
        }

        [HttpGet]
        public IActionResult MyCertificates()
        {
  
            return View();
        }
    }
}