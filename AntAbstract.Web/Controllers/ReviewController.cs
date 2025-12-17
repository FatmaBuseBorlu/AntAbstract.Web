using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Referee, Admin")]
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReviewController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var assignments = await _context.ReviewAssignments
                .Include(ra => ra.Submission)
                .ThenInclude(s => s.Conference)
                .Include(ra => ra.Review) 
                .Where(ra => ra.ReviewerId == userId)
                .OrderByDescending(ra => ra.AssignedDate)
                .ToListAsync();

            return View(assignments);
        }

        [HttpGet]
        public async Task<IActionResult> Evaluate(int id)
        {
            var userId = _userManager.GetUserId(User);

            var assignment = await _context.ReviewAssignments
                .Include(ra => ra.Submission).ThenInclude(s => s.Files) 
                .Include(ra => ra.Submission).ThenInclude(s => s.Conference)
                .Include(ra => ra.Review) 
                .FirstOrDefaultAsync(ra => ra.Id == id && ra.ReviewerId == userId);

            if (assignment == null)
            {
                TempData["ErrorMessage"] = "Bu bildiriye erişim yetkiniz yok veya atama bulunamadı.";
                return RedirectToAction("Index");
            }

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Evaluate(int assignmentId, string comments, string recommendation, int score)
        {
            var userId = _userManager.GetUserId(User);

            var assignment = await _context.ReviewAssignments
                .Include(ra => ra.Review)
                .Include(ra => ra.Submission)
                .FirstOrDefaultAsync(ra => ra.Id == assignmentId && ra.ReviewerId == userId);

            if (assignment == null) return NotFound();

            if (assignment.Review == null)
            {
                var review = new Review
                {
                    ReviewerName = User.Identity.Name ?? "Hakem",
                    CommentsToAuthor = comments,
                    Recommendation = recommendation, 
                    Score = score,
                    ReviewedAt = DateTime.UtcNow
                };

                assignment.Review = review;

            }
            else
            {
                assignment.Review.CommentsToAuthor = comments;
                assignment.Review.Recommendation = recommendation;
                assignment.Review.Score = score;
                assignment.Review.ReviewedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla kaydedildi.";

            return RedirectToAction(nameof(Index));
        }
    }
}