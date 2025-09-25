using AntAbstract.Domain.Entities; 
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System; 
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    // Bu controller'a sadece "Reviewer" rolündeki kullanıcılar erişebilir.
    [Authorize(Roles = "Reviewer")]
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ReviewController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: Review/Index
        // Hakemin kendisine atanmış görevleri listeler.
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen bir kongre URL'i (/slug) üzerinden işlem yapın.";
                return RedirectToAction("Index", "Home");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var assignments = await _context.ReviewAssignments
                .Where(a => a.Reviewer.AppUserId == userId && a.Reviewer.Conference.TenantId == _tenantContext.Current.Id)
                .Include(a => a.Submission)
                .ToListAsync();

            return View(assignments);
        }

        // --- ✅ YENİ EKLENEN METOTLAR ---

        // GET: Review/Evaluate/5
        // Değerlendirme formunu gösterir
        public async Task<IActionResult> Evaluate(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.ReviewAssignments
                .Include(a => a.Submission)
                .FirstOrDefaultAsync(a => a.ReviewAssignmentId == id);

            if (assignment == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reviewer = await _context.Reviewers.FirstOrDefaultAsync(r => r.Id == assignment.ReviewerId);
            if (reviewer?.AppUserId != userId)
            {
                return Forbid();
            }

            return View(assignment);
        }

        // POST: Review/Evaluate
        // Hakemin gönderdiği değerlendirme formunu kaydeder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Evaluate(Guid assignmentId, string recommendation, int score, string commentsToAuthor, string confidentialComments)
        {
            // 1. Değerlendirme kaydını oluştur
            var newReview = new Review
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignmentId,
                Recommendation = recommendation,
                Score = score,
                CommentsToAuthor = commentsToAuthor,
                ConfidentialComments = confidentialComments,
                CompletedDate = DateTime.UtcNow
            };

            // 2. Atama kaydının durumunu güncelle
            var assignment = await _context.ReviewAssignments.FindAsync(assignmentId);
            if (assignment != null)
            {
                assignment.Status = "Değerlendirildi";
            }

            // 3. Yeni değerlendirmeyi veritabanına ekle
            _context.Reviews.Add(newReview);

            // 4. Tüm değişiklikleri kaydet
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla gönderildi.";
            return RedirectToAction(nameof(Index));
        }
    }
}