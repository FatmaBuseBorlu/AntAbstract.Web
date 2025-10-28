using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
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
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;

        public DashboardController(AppDbContext context, TenantContext tenantContext, UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("Organizator"))
            {
                // --- ORGANİZATÖR PANELİ ---
                var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
                if (conference == null) return View("Error");

                var allSubmissions = _context.Submissions.Where(s => s.ConferenceId == conference.Id);

                // ✅ YENİ EKLENDİ: Grafik verisini hazırlayan sorgu
                var submissionStats = await allSubmissions
                    .Where(s => s.FinalDecision != null) // Sadece kararı verilmiş olanları say
                    .GroupBy(s => s.FinalDecision)
                    .Select(g => new { Decision = g.Key, Count = g.Count() })
                    .ToListAsync();

                var viewModel = new DashboardViewModel
                {
                    TotalUsers = await _userManager.Users.CountAsync(),
                    TotalSubmissions = await allSubmissions.CountAsync(),
                    TotalReviews = await _context.ReviewAssignments.CountAsync(ra => ra.Submission.ConferenceId == conference.Id),
                    // ✅ YENİ EKLENDİ: Grafik verilerini ViewModel'a ata
                    ChartLabels = submissionStats.Select(s => s.Decision).ToList(),
                    ChartData = submissionStats.Select(s => s.Count).ToList()
                };

                return View("Index", viewModel);
            }
            else if (User.IsInRole("Reviewer"))
            {
                // --- HAKEM PANELİ ---
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var allAssignments = _context.ReviewAssignments.Where(ra => ra.ReviewerId == currentUserId);

                var pendingList = await allAssignments
                    .Where(ra => ra.Status != "Completed")
                    .Include(ra => ra.Submission)
                    .OrderByDescending(ra => ra.AssignedDate)
                    .Take(5)
                    .ToListAsync();

                var viewModel = new ReviewerDashboardViewModel
                {
                    TotalAssigned = await allAssignments.CountAsync(),
                    CompletedReviews = await allAssignments.CountAsync(ra => ra.Status == "Completed"),
                    PendingReviews = await allAssignments.CountAsync(ra => ra.Status != "Completed"),
                    PendingAssignments = pendingList
                };

                return View("ReviewerDashboard", viewModel);
            }
            else
            {
                // --- Yazar gibi diğer kullanıcılar ---
                return RedirectToAction("Index", "Submission");
            }
        }
    }
}