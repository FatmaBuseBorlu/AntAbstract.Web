using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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

            // 1. Giriş yapmış olan kullanıcının ID'sini al
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. O anki URL'ye ait Konferans'ı bul
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return View(new List<ReviewAssignment>());
            }

            // 3. Giriş yapan kullanıcının BU KONFERANSTAKİ hakem kaydını bul
            var reviewer = await _context.Reviewers
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.AppUserId == userId && r.ConferenceId == conference.Id);

            if (reviewer == null)
            {
                return View(new List<ReviewAssignment>());
            }

            // 4. Sadece o hakemin ID'si ile görevleri ara (Daha basit ve güvenilir sorgu)
            var assignments = await _context.ReviewAssignments
                .Where(a => a.ReviewerId == reviewer.Id)
                .Include(a => a.Submission)
                .ToListAsync();

            return View(assignments);
        }

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

            var assignment = await _context.ReviewAssignments.FindAsync(assignmentId);
            if (assignment != null)
            {
                assignment.Status = "Değerlendirildi";
            }

            _context.Reviews.Add(newReview);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla gönderildi.";
            return RedirectToAction(nameof(Index));
        }
        // BU METOT SADECE TEST AMAÇLIDIR
        public async Task<IActionResult> TestQuery()
        {
            // Giriş yapmış kullanıcının ID'sini al
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // URL'deki tenant'ın slug'ını al
            var tenantSlug = _tenantContext.Current?.Slug;

            if (userId == null || tenantSlug == null)
            {
                return Content("Kullanıcı veya Tenant bulunamadı.");
            }

            // Entity Framework'ü atlayan ham SQL sorgusu
            var sql = @"
        SELECT 
            s.Title,
            ra.Status
        FROM ReviewAssignments AS ra
        INNER JOIN Reviewers AS r ON ra.ReviewerId = r.Id
        INNER JOIN Submissions AS s ON ra.SubmissionId = s.SubmissionId
        INNER JOIN Conferences AS c ON r.ConferenceId = c.Id
        INNER JOIN Tenants AS t ON c.TenantId = t.Id
        WHERE
            r.AppUserId = @p_userId
            AND t.Slug = @p_tenantSlug";

            System.Diagnostics.Debug.WriteLine("--- HAM SQL TESTİ BAŞLADI ---");

            // Veritabanı bağlantısını al ve sorguyu çalıştır
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;

                    // Parametreleri ekle (SQL Injection'ı önlemek için)
                    var userIdParam = command.CreateParameter();
                    userIdParam.ParameterName = "@p_userId";
                    userIdParam.Value = userId;
                    command.Parameters.Add(userIdParam);

                    var tenantSlugParam = command.CreateParameter();
                    tenantSlugParam.ParameterName = "@p_tenantSlug";
                    tenantSlugParam.Value = tenantSlug;
                    command.Parameters.Add(tenantSlugParam);

                    // Sorguyu çalıştır ve sonuçları oku
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.HasRows)
                        {
                            while (await reader.ReadAsync())
                            {
                                // Bulunan her bir görevi Output penceresine yazdır
                                var title = reader.GetString(0);
                                var status = reader.GetString(1);
                                System.Diagnostics.Debug.WriteLine($"BULUNAN GÖREV: Başlık='{title}', Durum='{status}'");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("HİÇ GÖREV BULUNAMADI.");
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("--- HAM SQL TESTİ BİTTİ ---");

            // Tarayıcıya basit bir mesaj gönder
            return Content("SQL Testi tamamlandı. Lütfen Visual Studio Output penceresini kontrol et.");
        }
    }
}