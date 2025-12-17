using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
// Eger EmailService yoksa bu satiri ve constructor'daki parametreyi silebilirsin
// using AntAbstract.Infrastructure.Services; 

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin")] // Organizator rolünü kaldırdım, sadece Admin kalsın şimdilik
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        // TenantContext yoksa silebilirsin, varsa kalsın
        // private readonly TenantContext _tenantContext; 
        private readonly UserManager<AppUser> _userManager;

        public DecisionController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Eğer Tenant (Çoklu Firma) yapısı kullanmıyorsan direkt ilk kongreyi alabilirsin
            // Veya aktif kongreyi bulma mantığını buraya yazabilirsin.
            var conference = await _context.Conferences.OrderByDescending(c => c.StartDate).FirstOrDefaultAsync();

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Sistemde aktif bir kongre bulunamadı.";
                return View(new DecisionIndexViewModel());
            }

            var allSubmissions = _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Review) // Review'i include etmeliyiz
                .Where(s => s.ConferenceId == conference.Id);

            // KARAR BEKLEYENLER:
            // Statüsü "İnceleniyor" olan VE Hakem atanmış VE Tüm hakemler puan vermiş (Review != null)
            var awaitingDecision = await allSubmissions
                .Where(s => s.Status == SubmissionStatus.UnderReview
                            && s.ReviewAssignments.Any()
                            && s.ReviewAssignments.All(ra => ra.Review != null))
                .ToListAsync();

            // KARAR VERİLMİŞLER:
            // Kabul veya Red durumunda olanlar
            var decided = await allSubmissions
                .Where(s => s.Status == SubmissionStatus.Accepted || s.Status == SubmissionStatus.Rejected || s.Status == SubmissionStatus.RevisionRequired)
                .ToListAsync();

            var viewModel = new DecisionIndexViewModel
            {
                AwaitingDecision = awaitingDecision,
                AlreadyDecided = decided
            };

            return View(viewModel);
        }

        // KARAR VERME İŞLEMİ (Kabul / Red)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(Guid submissionId, string decision, string note)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null) return NotFound();

            if (decision == "Accept")
            {
                submission.Status = SubmissionStatus.Accepted;
                // Opsiyonel: Yazara kabul maili gönder
            }
            else if (decision == "Reject")
            {
                submission.Status = SubmissionStatus.Rejected;
                // Opsiyonel: Yazara red maili gönder
            }
            else if (decision == "Revision")
            {
                submission.Status = SubmissionStatus.RevisionRequired;
            }

            submission.DecisionDate = DateTime.UtcNow;

            // Eğer yönetici notunu kaydetmek istersen Notification veya Submission tablosuna ekleyebilirsin

            // Bildirim Oluştur
            var notification = new Notification
            {
                UserId = submission.AuthorId,
                Title = "Bildiri Sonucu Açıklandı",
                Message = $"'{submission.Title}' başlıklı bildiriniz için karar: {decision}",
                Link = $"/Submission/Details/{submission.SubmissionId}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Karar başarıyla kaydedildi.";

            return RedirectToAction(nameof(Index));
        }
    }
}