using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public DecisionController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var allSubmissions = _context.Submissions
                .Include(s => s.Author) // Düzeltildi: User -> Author
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Review)
                .OrderByDescending(s => s.CreatedDate)
                .AsQueryable();

            // Enum Karşılaştırması: SubmissionStatus.Pending (0)
            var awaitingDecision = await allSubmissions
                .Where(s => s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.UnderReview)
                .ToListAsync();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(Guid submissionId, string decision, string note)
        {
            // ID düzeltmesi: submissionId ismini kullanıyoruz
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null) return NotFound();

            string kararMetni = "";

            if (decision == "Accept")
            {
                submission.Status = SubmissionStatus.Accepted; // Enum kullanıyoruz
                kararMetni = "Kabul Edildi";
            }
            else if (decision == "Reject")
            {
                submission.Status = SubmissionStatus.Rejected;
                kararMetni = "Reddedildi";
            }
            else if (decision == "Revision")
            {
                submission.Status = SubmissionStatus.RevisionRequired;
                kararMetni = "Revizyon İstendi";
            }

            submission.DecisionDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Bildiri başarıyla {kararMetni}.";
            return RedirectToAction(nameof(Index));
        }
    }
}