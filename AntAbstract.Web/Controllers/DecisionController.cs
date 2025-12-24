using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
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

        public DecisionController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        [HttpGet("/admin/decision")]
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
                Lead = "Karar ekranına geçmek için önce kongre seçin.",
                PostUrl = "/admin/decision/select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/admin/decision/select")]
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

        [HttpGet("/{slug}/admin/decision")]
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

            var allSubmissions = _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
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
        public IActionResult LegacyRoot()
        {
            return Redirect("/admin/decision");
        }

        [HttpGet("/{slug}/Decision/Index")]
        public IActionResult LegacyTenant(string slug)
        {
            return Redirect($"/{slug}/admin/decision");
        }

        [HttpPost("/{slug}/admin/decision/makedecision")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(string slug, Guid submissionId, string decision, string note)
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Tenant uyuşmuyor.";
                return RedirectToAction(nameof(SelectConference));
            }

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound();

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == submission.ConferenceId && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Bildiri bu tenant’a ait bir kongreye bağlı değil.";
                return RedirectToAction(nameof(SelectConference));
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
            return RedirectToAction(nameof(Index), new { slug, conferenceId = submission.ConferenceId });
        }
    }
}
