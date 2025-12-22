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
    public class ConferenceFlowController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ConferenceFlowController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // 1) Kongre seç (slug yok)
        [HttpGet("/admin/conferenceflow")]
        public async Task<IActionResult> SelectConference()
        {
            var conferences = await _context.Conferences
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View("SelectConference", conferences);
        }

        // 2) Seç -> slug ile akış ekranına git
        [HttpPost("/admin/conferenceflow/select")]
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

            return Redirect($"/{conf.Tenant.Slug}/admin/conferenceflow?conferenceId={conf.Id}");
        }

        // 3) Akış ekranı (slug var)
        [HttpGet("/{slug}/admin/conferenceflow")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            // Tenant middleware Current'ı slug üzerinden set ediyor olmalı
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            if (conferenceId == null)
            {
                TempData["ErrorMessage"] = "Lütfen önce kongre seçin.";
                return RedirectToAction(nameof(SelectConference));
            }

            // Güvenlik: seçilen kongre bu tenant'a ait mi?
            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == conferenceId && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Seçilen kongre bulunamadı veya bu tenant'a ait değil.";
                return RedirectToAction(nameof(SelectConference));
            }

            // Metrikler
            var submissionIds = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id)
                .Select(s => s.Id)
                .ToListAsync();

            var submissionCount = submissionIds.Count;

            var assignedSubmissionCount = submissionCount == 0
                ? 0
                : await _context.ReviewAssignments
                    .Where(ra => submissionIds.Contains(ra.SubmissionId))
                    .Select(ra => ra.SubmissionId)
                    .Distinct()
                    .CountAsync();

            var decidedSubmissionCount = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id && s.DecisionDate != null)
                .CountAsync();

            var vm = new ConferenceFlowIndexViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                Slug = slug,
                SubmissionCount = submissionCount,
                AssignedSubmissionCount = assignedSubmissionCount,
                DecidedSubmissionCount = decidedSubmissionCount
            };

            return View(vm);
        }
    }
}
