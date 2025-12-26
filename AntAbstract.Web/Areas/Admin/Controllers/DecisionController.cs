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
    [Authorize(Roles = "Admin")]
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public DecisionController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/Decision")]
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
                    return Redirect($"/{selectedConf.Tenant.Slug}/Admin/Decision?conferenceId={selectedConf.Id}");
                }
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Kongre Seç",
                Lead = "Karar ekranına geçmek için önce kongre seçin.",
                PostUrl = "/Admin/Decision/Select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Decision/Select")]
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
                return Redirect("/Admin/Decision");
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Admin/Decision?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/Decision")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return Redirect("/Admin/Decision");
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Tenant uyuşmuyor. Lütfen tekrar kongre seçin.";
                return Redirect("/Admin/Decision");
            }

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();
            if (conferenceId == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return Redirect("/Admin/Decision");
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bulunamadı veya bu tenant'a ait değil.";
                return Redirect("/Admin/Decision");
            }

            var allSubmissions = _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Reviewer)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Review)
                .OrderByDescending(s => s.CreatedDate)
                .AsQueryable();

            var awaitingDecision = await allSubmissions
                .Where(s => s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.UnderReview)
                .ToListAsync();

            var decided = await allSubmissions
                .Where(s => s.Status == SubmissionStatus.Accepted
                         || s.Status == SubmissionStatus.Rejected
                         || s.Status == SubmissionStatus.RevisionRequired)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;

            var viewModel = new DecisionIndexViewModel
            {
                AwaitingDecision = awaitingDecision,
                AlreadyDecided = decided
            };

            return View("~/Areas/Admin/Views/Decision/Index.cshtml", viewModel);
        }

        [HttpGet("/Decision/Index")]
        public IActionResult LegacyRoot() => Redirect("/Admin/Decision");

        [HttpGet("/{slug}/Decision/Index")]
        public IActionResult LegacyTenant(string slug) => Redirect($"/{slug}/Admin/Decision");

        [HttpPost("/{slug}/Admin/Decision/MakeDecision")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(string slug, Guid submissionId, string decision, string note)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return Redirect("/Admin/Decision");
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Tenant uyuşmuyor.";
                return Redirect("/Admin/Decision");
            }

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null)
                return NotFound();

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == submission.ConferenceId && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Bildiri bu tenant’a ait bir kongreye bağlı değil.";
                return Redirect("/Admin/Decision");
            }

            string kararMetni;

            if (decision == "Accept")
            {
                submission.Status = SubmissionStatus.Accepted;
                kararMetni = "Kabul Edildi";
            }
            else if (decision == "Reject")
            {
                submission.Status = SubmissionStatus.Rejected;
                kararMetni = "Reddedildi";
            }
            else
            {
                submission.Status = SubmissionStatus.RevisionRequired;
                kararMetni = "Revizyon İstendi";
            }

            submission.DecisionDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Bildiri başarıyla {kararMetni}.";
            return Redirect($"/{slug}/Admin/Decision?conferenceId={submission.ConferenceId}");
        }
    }
}
