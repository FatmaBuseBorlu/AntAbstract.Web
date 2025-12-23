using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Area("Admin")]
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

        [HttpGet("/admin/reports")]
        public async Task<IActionResult> SelectConference()
        {
            var conferences = await _context.Conferences
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View("SelectConference", conferences);
        }

        [HttpPost("/admin/reports/select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId)
        {
            var conf = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            return Redirect($"/{conf.Tenant.Slug}/admin/reports?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/admin/reports")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null || conferenceId == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == conferenceId && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bulunamadı veya bu tenant'a ait değil.";
                return RedirectToAction(nameof(SelectConference));
            }

            var submissions = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id)
                .Select(s => new { s.Id, s.Status, s.DecisionDate })
                .ToListAsync();

            var submissionIds = submissions.Select(x => x.Id).ToList();

            var totalAssignments = submissionIds.Count == 0
                ? 0
                : await _context.ReviewAssignments
                    .Where(ra => submissionIds.Contains(ra.SubmissionId))
                    .CountAsync();

            var assignedSubmissions = submissionIds.Count == 0
                ? 0
                : await _context.ReviewAssignments
                    .Where(ra => submissionIds.Contains(ra.SubmissionId))
                    .Select(ra => ra.SubmissionId)
                    .Distinct()
                    .CountAsync();

            var decidedSubmissions = submissions.Count(x => x.DecisionDate != null);

            var vm = new ReportsIndexViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                Slug = slug,

                TotalSubmissions = submissions.Count,
                AssignedSubmissions = assignedSubmissions,
                DecidedSubmissions = decidedSubmissions,

                NewCount = submissions.Count(x => x.Status == SubmissionStatus.New),
                PendingCount = submissions.Count(x => x.Status == SubmissionStatus.Pending),
                UnderReviewCount = submissions.Count(x => x.Status == SubmissionStatus.UnderReview),

                AcceptedCount = submissions.Count(x => x.Status == SubmissionStatus.Accepted),
                RejectedCount = submissions.Count(x => x.Status == SubmissionStatus.Rejected),
                RevisionRequiredCount = submissions.Count(x => x.Status == SubmissionStatus.RevisionRequired),

                TotalAssignments = totalAssignments
            };

            return View(vm);
        }
    }
}
