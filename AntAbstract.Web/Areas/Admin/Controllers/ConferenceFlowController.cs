using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Assignment;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<AppUser> _userManager;

        public ConferenceFlowController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
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

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!isAdmin && user?.TenantId != null)
            {
                query = query.Where(c => c.TenantId == user.TenantId.Value);
            }
            else if (!isAdmin && user?.TenantId == null)
            {
                query = query.Where(c => false);
            }

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Kongre Seç",
                Lead = "Kongre akışını görüntülemek için lütfen bir kongre seçiniz.",
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
                TempData["ErrorMessage"] = "Seçilen kongre bulunamadı.";
                return Redirect("/Admin/ConferenceFlow");
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Admin/ConferenceFlow?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/ConferenceFlow")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Geçersiz organizasyon (tenant). Lütfen tekrar seçim yapın.";
                return Redirect("/Admin/ConferenceFlow");
            }

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();
            if (conferenceId == null)
            {
                return Redirect("/Admin/ConferenceFlow");
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bu organizasyona ait değil.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var submissionCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s => s.ConferenceId == conference.Id);

            var assignedSubmissionCount = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => ra.Submission.ConferenceId == conference.Id)
                .Select(ra => ra.SubmissionId)
                .Distinct()
                .CountAsync();

            var decidedSubmissionCount = await _context.Submissions
                .AsNoTracking()
                .CountAsync(s => s.ConferenceId == conference.Id && s.DecisionDate != null);

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