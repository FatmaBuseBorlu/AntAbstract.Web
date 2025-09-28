using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic; // List için eklendi
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public DecisionController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: Decision/Index
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Konferans bulunamadı.";
                // Boş bir ViewModel ile View'ı göster
                return View(new Web.Models.ViewModels.DecisionIndexViewModel
                {
                    AwaitingDecision = new List<Submission>(),
                    AlreadyDecided = new List<Submission>()
                });
            }

            // 1. Karar BEKLEYEN özetleri bul
            var submissionsAwaitingDecision = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                .Where(s => s.ConferenceId == conference.Id &&
                             s.FinalDecision == null &&
                             s.ReviewAssignments.Any() &&
                             s.ReviewAssignments.All(ra => ra.Status == "Değerlendirildi"))
                .ToListAsync();

            // 2. Kararı VERİLMİŞ özetleri bul
            var submissionsAlreadyDecided = await _context.Submissions
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == conference.Id &&
                             s.FinalDecision != null) // Nihai kararı olanlar
                .ToListAsync();

            // 3. İki listeyi de ViewModel'e koy
            var viewModel = new Web.Models.ViewModels.DecisionIndexViewModel
            {
                AwaitingDecision = submissionsAwaitingDecision,
                AlreadyDecided = submissionsAlreadyDecided
            };

            // 4. ViewModel'i sayfaya gönder
            return View(viewModel);
        }

        // GET: Decision/MakeDecision/5
        public async Task<IActionResult> MakeDecision(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Bu sorgu, hem özeti, hem o özete yapılan atamaları, 
            // hem de o atamalara yapılan DEĞERLENDİRMELERİ getirir.
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Review) // Atamaların içindeki Değerlendirmeleri de dahil et
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null)
            {
                return NotFound();
            }

            return View(submission);
        }
        // POST: Decision/MakeDecision
        // Organizatörün verdiği nihai kararı veritabanına kaydeder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(Guid submissionId, string finalDecision)
        {
            if (submissionId == Guid.Empty || string.IsNullOrEmpty(finalDecision))
            {
                // Gerekli bilgiler gelmediyse hata ver.
                TempData["ErrorMessage"] = "Geçersiz bir seçim yaptınız.";
                return RedirectToAction(nameof(Index));
            }

            // Güncellenecek olan özeti bul
            var submissionToUpdate = await _context.Submissions.FindAsync(submissionId);

            if (submissionToUpdate == null)
            {
                return NotFound();
            }

            // Karar alanlarını güncelle
            submissionToUpdate.FinalDecision = finalDecision;
            submissionToUpdate.DecisionDate = DateTime.UtcNow;

            // Değişiklikleri kaydet
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"'{submissionToUpdate.Title}' başlıklı özet için karar başarıyla kaydedildi.";
            return RedirectToAction(nameof(Index));
        }
    }
}