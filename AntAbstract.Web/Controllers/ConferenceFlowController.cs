using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ConferenceFlowController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ConferenceFlowController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        [HttpGet("/admin/conferenceflow")]
        public async Task<IActionResult> SelectConference()
        {
            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Kongre Seç",
                Lead = "Kongre akışını görüntülemek için önce kongre seçin.",
                PostUrl = "/admin/conferenceflow/select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/admin/conferenceflow/select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId)
        {
            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            return RedirectToAction(nameof(Index), new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
        }

        [HttpGet("/{slug}/admin/conferenceflow")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null || conferenceId == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Tenant uyuşmuyor. Lütfen tekrar kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bulunamadı veya bu tenant'a ait değil.";
                return RedirectToAction(nameof(SelectConference));
            }

            var submissionQuery = _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id);

            var submissionIds = await submissionQuery
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

            var decidedSubmissionCount = await submissionQuery
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
    }
}
