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
using AntAbstract.Infrastructure.Services;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IEmailService _emailService;
        private readonly UserManager<AppUser> _userManager;

        public DecisionController(AppDbContext context, TenantContext tenantContext, IEmailService emailService, UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _userManager = userManager;
        }

        // GET: Decision
        public async Task<IActionResult> Index()
        {
            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Konferans bulunamadı.";
                return View(new DecisionIndexViewModel { AwaitingDecision = new List<Submission>(), AlreadyDecided = new List<Submission>() });
            }

            var allSubmissions = _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                .Where(s => s.ConferenceId == conference.Id);

            var awaitingDecision = await allSubmissions
                .Where(s => s.FinalDecision == null && s.ReviewAssignments.Any() && s.ReviewAssignments.All(ra => ra.Status == "Completed"))
                .ToListAsync();

            var decided = await allSubmissions
                .Where(s => s.FinalDecision != null)
                .ToListAsync();

            var viewModel = new DecisionIndexViewModel
            {
                AwaitingDecision = awaitingDecision,
                AlreadyDecided = decided // Model ismini Decided olarak değiştirdim
            };

            return View(viewModel);
        }

        // GET: Decision/MakeDecision/5
        public async Task<IActionResult> MakeDecision(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.AppUser) // Hakem bilgilerini (AppUser) de yükle
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Review)
                        .ThenInclude(r => r.Answers)
                            .ThenInclude(a => a.Criterion)
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null) return NotFound();

            return View(submission);
        }

        // POST: Decision/MakeDecision
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(Guid submissionId, string finalDecision)
        {
            if (submissionId == Guid.Empty || string.IsNullOrEmpty(finalDecision))
            {
                TempData["ErrorMessage"] = "Geçersiz bir seçim yaptınız.";
                return RedirectToAction(nameof(Index));
            }

            var submissionToUpdate = await _context.Submissions.FindAsync(submissionId);
            if (submissionToUpdate == null) return NotFound();

            submissionToUpdate.FinalDecision = finalDecision;
            submissionToUpdate.DecisionDate = DateTime.UtcNow;
            submissionToUpdate.Status = finalDecision;

            await _context.SaveChangesAsync();

            try
            {
                var author = await _userManager.FindByIdAsync(submissionToUpdate.AuthorId);
                if (author != null && !string.IsNullOrEmpty(author.Email))
                {
                    var subject = $"Özetiniz Hakkında Karar Verildi: {submissionToUpdate.FinalDecision}";
                    var message = $"<h1>Merhaba {author.DisplayName},</h1><p>'{submissionToUpdate.Title}' başlıklı özetiniz hakkında kongre komitesi tarafından bir karar verilmiştir.</p><p><strong>Nihai Karar: {submissionToUpdate.FinalDecision}</strong></p><p>Detayları görmek için sisteme giriş yapabilirsiniz.</p>";
                    await _emailService.SendEmailAsync(author.Email, subject, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YAZARA KARAR E-POSTASI GÖNDERİM HATASI: {ex.Message}");
            }

            TempData["SuccessMessage"] = $"'{submissionToUpdate.Title}' başlıklı özet için karar başarıyla kaydedildi.";
            return RedirectToAction(nameof(Index));
        }
    }
}