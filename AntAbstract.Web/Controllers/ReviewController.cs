using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Reviewer")] // Bu sayfa sadece Hakemler içindir.
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReviewController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Review
        // O anki hakeme atanmış tüm değerlendirme görevlerini listeler.
        public async Task<IActionResult> Index()
        {
            // O anki kullanıcının ID'sini al (bu bir string).
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Veritabanındaki ReviewAssignment.ReviewerId (artık string) ile eşleştir.
            var assignments = await _context.ReviewAssignments
                .Where(ra => ra.ReviewerId == currentUserId)
                .Include(ra => ra.Submission) // Özet bilgilerini de getir
                .OrderByDescending(ra => ra.AssignedDate)
                .ToListAsync();

            return View(assignments);
        }
        // ReviewController.cs içine eklenecek

        // GET: /Review/PerformReview/5
        public async Task<IActionResult> PerformReview(Guid id) // id = ReviewAssignmentId
        {
            // 1. İlgili atamayı, özeti ve kongre bilgisiyle birlikte getir.
            var assignment = await _context.ReviewAssignments
                .Include(a => a.Submission)
                .ThenInclude(s => s.Conference)
                .FirstOrDefaultAsync(a => a.ReviewAssignmentId == id);

            if (assignment == null) return NotFound();

            // 2. Bu kongreye ait Değerlendirme Formunu bul.
            var reviewForm = await _context.ReviewForms
                .Include(f => f.Criteria) // Formun kriterlerini (sorularını) da getir.
                .FirstOrDefaultAsync(f => f.ConferenceId == assignment.Submission.ConferenceId);

            if (reviewForm == null)
            {
                // Eğer bu kongre için özel bir form oluşturulmamışsa, bir hata göster.
                return Content("Bu kongre için henüz bir değerlendirme formu oluşturulmamıştır.");
            }

            var viewModel = new PerformReviewViewModel
            {
                Assignment = assignment,
                Criteria = reviewForm.Criteria.OrderBy(c => c.DisplayOrder).ToList(),
                Answers = new List<ReviewAnswer>() // Cevap listesini boş başlat
            };

            return View(viewModel);
        }
        // ReviewController.cs içine eklenecek

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PerformReview(PerformReviewViewModel viewModel)
        {
            // 1. Orijinal atama görevini veritabanından bul.
            var assignment = await _context.ReviewAssignments.FindAsync(viewModel.Assignment.ReviewAssignmentId);
            if (assignment == null)
            {
                return NotFound();
            }

            // 2. Bu değerlendirmeye ait tüm cevapları içerecek olan ana "Review" nesnesini oluştur.
            var newReview = new Review
            {
                ReviewAssignmentId = assignment.ReviewAssignmentId,
                ReviewDate = DateTime.UtcNow
            };
            _context.Reviews.Add(newReview);
            // Ana Review nesnesini kaydet ki ID'si oluşsun.
            await _context.SaveChangesAsync();

            // 3. Formdan gelen her bir cevabı işle.
            foreach (var answer in viewModel.Answers)
            {
                // Cevabı, az önce oluşturduğumuz ana Review nesnesine bağla.
                answer.ReviewId = newReview.Id;
                _context.ReviewAnswers.Add(answer);
            }

            // 4. Atama görevinin durumunu "Tamamlandı" olarak güncelle.
            assignment.Status = "Completed";
            _context.ReviewAssignments.Update(assignment);

            // 5. Tüm değişiklikleri (yeni Review, yeni Cevaplar, güncellenen Görev) tek seferde veritabanına kaydet.
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla gönderildi. Teşekkür ederiz!";
            return RedirectToAction("Index"); // Hakemi "Değerlendirmelerim" listesine geri yönlendir.
        }
    }
}