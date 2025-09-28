using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Web.Models.ViewModels;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ReportsController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen raporları görmek için bir kongre seçin.";
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                return View(new ReportsViewModel());
            }

            var submissions = _context.Submissions.Where(s => s.ConferenceId == conference.Id);

            var viewModel = new ReportsViewModel
            {
                TotalSubmissions = await submissions.CountAsync(),
                TotalReviewers = await _context.Reviewers.CountAsync(r => r.ConferenceId == conference.Id && r.IsActive),

                // Yeni istatistikler
                AcceptedSubmissions = await submissions.CountAsync(s => s.FinalDecision == "Kabul Edildi"),
                RejectedSubmissions = await submissions.CountAsync(s => s.FinalDecision == "Reddedildi"),
                RevisionSubmissions = await submissions.CountAsync(s => s.Status.Contains("Revizyon")),

                AwaitingDecisionSubmissions = await submissions
                    .CountAsync(s => s.FinalDecision == null && (s.ReviewAssignments.Any() && s.ReviewAssignments.All(ra => ra.Status == "Değerlendirildi") || s.Status == "Revizyon Tamamlandı")),

                AwaitingAssignmentSubmissions = await submissions.CountAsync(s => !s.ReviewAssignments.Any())
            };

            return View(viewModel);
        }
    }
}