using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class AssignmentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IEmailService _emailService;
        private readonly IReviewerRecommendationService _recommendationService;
        private readonly UserManager<AppUser> _userManager;

        public AssignmentController(AppDbContext context,
                                            TenantContext tenantContext,
                                            IEmailService emailService,
                                            UserManager<AppUser> userManager, 
                                            IReviewerRecommendationService recommendationService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _userManager = userManager;
            _recommendationService = recommendationService;
        }

        // GET: Assignment/Index (Bu metot sende zaten vardı)
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen bir kongre URL'i (/slug) üzerinden işlem yapın.";
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Bu kongre için veritabanında bir konferans kaydı bulunamadı.";
                return View(new List<Domain.Entities.Submission>());
            }

            var submissions = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .ToListAsync();

            return View(submissions);
        }

        // ------------------ ✅ YENİ METOTLARI BURAYA YAPIŞTIR ------------------

        // GET: Assignment/Assign/5
        // Belirli bir özete hakem atama formunu gösterir.
        public async Task<IActionResult> Assign(Guid id)
        {
            var submission = await _context.Submissions.Include(s => s.Author).FirstOrDefaultAsync(s => s.SubmissionId == id);
            if (submission == null) return NotFound();

            var recommendedReviewers = await _recommendationService.GetRecommendationsAsync(id);
            var allReviewers = await _userManager.GetUsersInRoleAsync("Reviewer");
            var allOtherReviewers = allReviewers.Except(recommendedReviewers).ToList();

            var viewModel = new AssignReviewerViewModel
            {
                Submission = submission,
                RecommendedReviewers = recommendedReviewers.ToList(),
                AllOtherReviewers = allOtherReviewers
            };

            return View(viewModel);
        }

        // POST: Assignment/Assign
        // AssignmentController.cs içine eklenecek

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignReviewer(Guid submissionId, string reviewerId)
        {
            // 1. Bu atamanın daha önce yapılıp yapılmadığını kontrol et
            var alreadyExists = await _context.ReviewAssignments
                .AnyAsync(ra => ra.SubmissionId == submissionId && ra.ReviewerId == reviewerId);

            if (alreadyExists)
            {
                TempData["ErrorMessage"] = "Bu hakem bu özete zaten atanmış.";
                return RedirectToAction("Assign", new { id = submissionId });
            }

            // 2. Yeni atama kaydını oluştur
            var newAssignment = new ReviewAssignment
            {
                ReviewAssignmentId = Guid.NewGuid(),
                SubmissionId = submissionId,
                ReviewerId = reviewerId,
                AssignedDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(14), // Örnek olarak 2 hafta süre verelim
                Status = "Bekleniyor"
            };

            _context.ReviewAssignments.Add(newAssignment);
            await _context.SaveChangesAsync();

            // 3. (İsteğe bağlı ama önerilir) Hakeme bilgilendirme e-postası gönder
            try
            {
                var reviewer = await _userManager.FindByIdAsync(reviewerId);
                var submission = await _context.Submissions.FindAsync(submissionId);
                if (reviewer != null && submission != null)
                {
                    var subject = "Yeni Değerlendirme Görevi";
                    var message = $"Merhaba {reviewer.DisplayName},<br><br>'{submission.Title}' başlıklı özeti değerlendirmeniz için görevlendirilmiş bulunmaktasınız. Sisteme giriş yaparak detayları görebilirsiniz.";
                    await _emailService.SendEmailAsync(reviewer.Email, subject, message);
                }
            }
            catch (Exception ex)
            {
                // E-posta hatası olursa logla ama programı durdurma
                System.Diagnostics.Debug.WriteLine($"E-POSTA GÖNDERİM HATASI (HAKEM ATAMA): {ex.Message}");
            }


            TempData["SuccessMessage"] = $"Hakem başarıyla atandı.";
            // Kullanıcıyı ana özet atama listesine geri yönlendir
            return RedirectToAction("Index");
        }

    } 
}