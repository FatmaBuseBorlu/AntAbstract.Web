using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public ReportsController(AppDbContext context, TenantContext tenantContext, ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/admin/reports")]
        public async Task<IActionResult> SelectConference()
        {
            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Raporlama Merkezi",
                Lead = "Verilerini incelemek istediğiniz kongreyi seçerek devam edin.",
                PostUrl = "/admin/reports/select",
                SubmitText = "Raporları Görüntüle",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/admin/reports/select")]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId)
        {
            var conf = await _context.Conferences.Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null) return NotFound();

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);

            return RedirectToAction("Index", new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
        }

        [HttpGet("/{slug}/admin/reports")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {

            if (_tenantContext.Current == null)
                return RedirectToAction(nameof(SelectConference));

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();

            if (conferenceId == null)
                return RedirectToAction(nameof(SelectConference));

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference));

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return RedirectToAction(nameof(SelectConference));

            var submissions = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Select(s => new { s.Id, s.Status, s.DecisionDate })
                .ToListAsync();

            var submissionIds = submissions.Select(x => x.Id).ToList();

            var totalAssignments = submissionIds.Count == 0 ? 0 :
                await _context.ReviewAssignments
                    .AsNoTracking()
                    .Where(ra => submissionIds.Contains(ra.SubmissionId))
                    .CountAsync();

            var assignedSubmissions = submissionIds.Count == 0 ? 0 :
                await _context.ReviewAssignments
                    .AsNoTracking()
                    .Where(ra => submissionIds.Contains(ra.SubmissionId))
                    .Select(ra => ra.SubmissionId)
                    .Distinct()
                    .CountAsync();

            var registrations = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == conference.Id)
                .Select(r => new { r.Amount, r.IsPaid })
                .ToListAsync();

            var vm = new ReportsIndexViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                ConferenceName = conference.Title,
                Slug = slug,

                TotalSubmissions = submissions.Count,
                AssignedSubmissions = assignedSubmissions,
                DecidedSubmissions = submissions.Count(x => x.DecisionDate != null),
                TotalAssignments = totalAssignments,

                TotalRegistrations = registrations.Count,
                TotalRevenue = registrations.Where(x => x.IsPaid).Sum(x => x.Amount),

                NewCount = submissions.Count(x => x.Status == SubmissionStatus.New),
                PendingCount = submissions.Count(x => x.Status == SubmissionStatus.Pending),
                UnderReviewCount = submissions.Count(x => x.Status == SubmissionStatus.UnderReview),
                AcceptedCount = submissions.Count(x => x.Status == SubmissionStatus.Accepted),
                RejectedCount = submissions.Count(x => x.Status == SubmissionStatus.Rejected),
                RevisionRequiredCount = submissions.Count(x => x.Status == SubmissionStatus.RevisionRequired)
            };

            return View("~/Areas/Admin/Views/Reports/Index.cshtml", vm);
        }

        [HttpGet("/Reports/Index")]
        public IActionResult LegacyRoot() => Redirect("/admin/reports");

        [HttpGet("/{slug}/Reports/Index")]
        public IActionResult LegacyTenant(string slug) => Redirect($"/{slug}/admin/reports");
    }
}