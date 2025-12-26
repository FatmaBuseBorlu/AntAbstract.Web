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
    public class ConferenceFlowController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public ConferenceFlowController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/ConferenceFlow")]
        public async Task<IActionResult> SelectConference()
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var selectedConf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", selectedConf.Tenant.Slug);
                    return Redirect($"/{selectedConf.Tenant.Slug}/Admin/ConferenceFlow?conferenceId={selectedConf.Id}");
                }
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Kongre Akışı",
                Lead = "Kongre akışını görüntülemek için önce kongre seçin.",
                PostUrl = "/Admin/ConferenceFlow/Select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/ConferenceFlow/Select")]
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
                return Redirect("/Admin/ConferenceFlow");
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Admin/ConferenceFlow?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return Redirect("/Admin/ConferenceFlow");
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Tenant uyuşmuyor. Lütfen tekrar kongre seçin.";
                return Redirect("/Admin/ConferenceFlow");
            }

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();

            if (conferenceId == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bulunamadı veya bu tenant'a ait değil.";
                return Redirect("/Admin/ConferenceFlow");
            }

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

        [HttpGet("/admin/conferenceflow")]
        public IActionResult LegacyRoot() => Redirect("/Admin/ConferenceFlow");

        [HttpGet("/{slug}/admin/conferenceflow")]
        public IActionResult LegacyTenant(string slug) => Redirect($"/{slug}/Admin/ConferenceFlow");
    }
}
