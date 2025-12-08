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
              
                .Where(s => s.Status == SubmissionStatus.UnderReview && s.ReviewAssignments.Any() && s.ReviewAssignments.All(ra => ra.Status == "Completed"))
                .ToListAsync();

            var decided = await allSubmissions
               
                .Where(s => s.Status != SubmissionStatus.New && s.Status != SubmissionStatus.UnderReview)
                .ToListAsync();

            var viewModel = new DecisionIndexViewModel
            {
                AwaitingDecision = awaitingDecision,
                AlreadyDecided = decided
            };

            return View(viewModel);
        }

        
        public async Task<IActionResult> MakeDecision(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.AppUser) 
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Review)
                        .ThenInclude(r => r.Answers)
                            .ThenInclude(a => a.Criterion)
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null) return NotFound();

            return View(submission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(Guid submissionId, string finalDecision)
        {
            if (submissionId == Guid.Empty || string.IsNullOrEmpty(finalDecision))
            {
                TempData["ErrorMessage"] = "Geçersiz bir seçim yaptınız.";
                return RedirectToAction(nameof(Index));
            }

            var submissionToUpdate = await _context.Submissions.Include(s => s.Author).FirstOrDefaultAsync(s => s.SubmissionId == submissionId);
            if (submissionToUpdate == null) return NotFound();

            if (Enum.TryParse<SubmissionStatus>(finalDecision, true, out var statusEnum))
            {
                submissionToUpdate.Status = statusEnum;
            }
            else
            {

                submissionToUpdate.Status = SubmissionStatus.Accepted; 
            }


            submissionToUpdate.FinalDecision = finalDecision; 
            submissionToUpdate.DecisionDate = DateTime.UtcNow;


            await _context.SaveChangesAsync();


            try
            {
                var author = submissionToUpdate.Author ?? await _userManager.FindByIdAsync(submissionToUpdate.AuthorId);
                if (author != null && !string.IsNullOrEmpty(author.Email))
                {
                    var subject = $"Özetiniz Hakkında Karar Verildi: {finalDecision}";
                    var message = $"<h1>Merhaba {author.FirstName},</h1><p>'{submissionToUpdate.Title}' başlıklı özetiniz hakkında kongre komitesi tarafından bir karar verilmiştir.</p><p><strong>Nihai Karar: {finalDecision}</strong></p><p>Detayları görmek için sisteme giriş yapabilirsiniz.</p>";
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