using AntAbstract.Web.Models.ViewModels.Admin.Assignment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ConferenceFlowController : AdminConferenceContextControllerBase
    {
        public ConferenceFlowController(
            AntAbstract.Infrastructure.Context.AppDbContext context,
            AntAbstract.Infrastructure.Context.TenantContext tenantContext,
            AntAbstract.Infrastructure.Services.ISelectedConferenceService selectedConferenceService)
            : base(context, tenantContext, selectedConferenceService)
        {
        }

        [HttpGet("/Admin/ConferenceFlow")]
        public async Task<IActionResult> Root()
        {
            return await GoSelectAsync(
                "/Admin/ConferenceFlow",
                "Kongre Akışı",
                "Kongre akışını görüntülemek için önce kongre seçin."
            );
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null)
                return await GoSelectAsync(
                    "/Admin/ConferenceFlow",
                    "Kongre Akışı",
                    "Kongre akışını görüntülemek için önce kongre seçin."
                );

            var submissionIds = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Select(s => s.Id)
                .ToListAsync();

            var submissionCount = submissionIds.Count;

            var assignedSubmissionCount = submissionCount == 0
                ? 0
                : await _context.ReviewAssignments
                    .AsNoTracking()
                    .Where(ra => submissionIds.Contains(ra.SubmissionId))
                    .Select(ra => ra.SubmissionId)
                    .Distinct()
                    .CountAsync();

            var decidedSubmissionCount = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .CountAsync(s => s.DecisionDate != null);

            var vm = new ConferenceFlowIndexViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                Slug = slug,
                SubmissionCount = submissionCount,
                AssignedSubmissionCount = assignedSubmissionCount,
                DecidedSubmissionCount = decidedSubmissionCount
            };

            return View("~/Areas/Admin/Views/ConferenceFlow/Index.cshtml", vm);
        }

        [HttpGet("/ConferenceFlow/Index")]
        public IActionResult LegacyRoot() => Redirect("/Admin/ConferenceFlow");

        [HttpGet("/{slug}/ConferenceFlow/Index")]
        public IActionResult LegacyTenant(string slug) => Redirect($"/{slug}/Admin/ConferenceFlow");
    }
}
