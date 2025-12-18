using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // 1. GÖREV LİSTESİ
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var assignments = await _context.ReviewAssignments
                .Include(ra => ra.Submission)
                .ThenInclude(s => s.Conference)
                .Include(ra => ra.Review) // Review dolu mu boş mu kontrol edeceğiz
                .Where(ra => ra.ReviewerId == userId)
                .OrderByDescending(ra => ra.AssignedDate)
                .ToListAsync();

            return View(assignments);
        }

        // 2. DEĞERLENDİRME EKRANI (GET)
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

        // 3. DEĞERLENDİRME KAYDETME (POST)
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

            // Yorum yoksa YENİ OLUŞTUR
            if (assignment.Review == null)
            {
                var review = new Review
                {
                    ReviewerName = User.Identity.Name ?? "Hakem",
                    CommentsToAuthor = comments,
                    Recommendation = recommendation,
                    Score = score,
                    ReviewedAt = DateTime.Now
                };
                assignment.Review = review;
            }
            // Varsa GÜNCELLE
            else
            {
                assignment.Review.CommentsToAuthor = comments;
                assignment.Review.Recommendation = recommendation;
                assignment.Review.Score = score;
                assignment.Review.ReviewedAt = DateTime.Now;
            }

            // HATA VEREN SATIR SİLİNDİ (assignment.Status = 1)
            // Review nesnesi eklendiği için artık işlem tamamlanmış sayılır.

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla kaydedildi.";

            return RedirectToAction(nameof(Index));
        }
    }
}