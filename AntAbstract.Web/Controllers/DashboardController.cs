using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public DashboardController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("Organizator"))
            {
                // --- ORGANİZATÖR PANELİ ---
                var tenantId = _tenantContext.Current?.Id;
                if (tenantId == null) return View("Error");

                var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.TenantId == tenantId);
                if (conference == null) return View("Error");

                var totalUsers = await _context.Users.CountAsync();
                var allSubmissions = _context.Submissions.Where(s => s.ConferenceId == conference.Id);
                var totalSubmissions = await allSubmissions.CountAsync();
                var totalReviews = await _context.ReviewAssignments.Where(ra => ra.Submission.ConferenceId == conference.Id).CountAsync();

                // ---  GRAFİK VERİSİNİ HAZIRLAMA BAŞLANGIÇ ---

                // 1. Özetleri nihai karar durumuna göre grupla ve her grubun sayısını al.
                var submissionStats = await allSubmissions
                    .Where(s => s.FinalDecision != null) // Sadece kararı verilmiş olanları say
                    .GroupBy(s => s.FinalDecision)
                    .Select(g => new { Decision = g.Key, Count = g.Count() })
                    .ToListAsync();

                // --- GRAFİK VERİSİNİ HAZIRLAMA BİTİŞ ---

                var viewModel = new DashboardViewModel
                {
                    TotalUsers = totalUsers,
                    TotalSubmissions = totalSubmissions,
                    TotalReviews = totalReviews,
                    //  ViewModel'a grafiğe özel verileri ekle
                    ChartLabels = submissionStats.Select(s => s.Decision).ToList(),
                    ChartData = submissionStats.Select(s => s.Count).ToList()
                };

                return View("Index", viewModel);
            }
            else if (User.IsInRole("Reviewer"))
            {
                // --- YENİ HAKEM PANELİ ---
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

                var currentUserIdGuid = Guid.Parse(currentUserId);

                var allAssignments = _context.ReviewAssignments.Where(ra => ra.ReviewerId == currentUserIdGuid);

                int total = await allAssignments.CountAsync();
                int completed = await allAssignments.CountAsync(ra => ra.Status == "Completed");

                // ✅ DÜZELTME: "AssignmentDate" -> "AssignedDate" olarak değiştirildi.
                var pendingList = await allAssignments
                    .Where(ra => ra.Status != "Completed")
                    .Include(ra => ra.Submission)
                    .OrderByDescending(ra => ra.AssignedDate) // <--- HATA BURADAYDI
                    .Take(5)
                    .ToListAsync();

                var viewModel = new ReviewerDashboardViewModel
                {
                    TotalAssigned = total,
                    CompletedReviews = completed,
                    PendingReviews = total - completed,
                    PendingAssignments = pendingList
                };

                return View("ReviewerDashboard", viewModel);
            }
            else
            {
                // --- Diğer kullanıcılar (Yazar vb.) ---
                return RedirectToAction("Index", "Submission");
            }
        }
    }
}