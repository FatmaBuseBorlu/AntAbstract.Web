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
    public class SessionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public SessionController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        private async Task<Conference?> GetConferenceOrNull(string? slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null) return null;
            if (string.IsNullOrWhiteSpace(slug)) return null;

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return null;

            conferenceId ??= _selectedConferenceService.GetSelectedConferenceId();
            if (conferenceId == null) return null;

            return await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId.Value && c.TenantId == _tenantContext.Current.Id);
        }

        [HttpGet("/Admin/Session")]
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
                    return Redirect($"/{selectedConf.Tenant.Slug}/Admin/Session?conferenceId={selectedConf.Id}");
                }
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Oturum Yönetimi",
                Lead = "Oturumları yönetmek istediğiniz kongreyi seçerek devam edin.",
                PostUrl = "/Admin/Session/Select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Session/Select")]
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
                return Redirect("/Admin/Session");
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Admin/Session?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/Session")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            var sessions = await _context.Sessions
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Submissions)
                .OrderBy(s => s.SessionDate)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;

            return View(sessions);
        }

        [HttpGet("/{slug}/Admin/Session/Create")]
        public async Task<IActionResult> Create(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;

            return View();
        }

        [HttpPost("/{slug}/Admin/Session/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Session session, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            if (!ModelState.IsValid)
            {
                ViewBag.ConferenceId = conference.Id;
                ViewBag.ConferenceName = conference.Title;
                return View(session);
            }

            session.Id = Guid.NewGuid();
            session.ConferenceId = conference.Id;

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Oturum başarıyla oluşturuldu.";
            return Redirect($"/{slug}/Admin/Session?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/Session/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(string slug, Guid id, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Id == id && s.ConferenceId == conference.Id);

            if (session == null) return NotFound();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;

            return View(session);
        }

        [HttpPost("/{slug}/Admin/Session/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string slug, Guid id, Session session, Guid? conferenceId)
        {
            if (id != session.Id) return NotFound();

            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            if (!ModelState.IsValid)
            {
                ViewBag.ConferenceId = conference.Id;
                ViewBag.ConferenceName = conference.Title;
                return View(session);
            }

            var existingSession = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Id == id && s.ConferenceId == conference.Id);

            if (existingSession == null) return NotFound();

            existingSession.Title = session.Title;
            existingSession.Location = session.Location;
            existingSession.SessionDate = session.SessionDate;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Oturum güncellendi.";

            return Redirect($"/{slug}/Admin/Session?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/Session/Manage/{id:guid}")]
        public async Task<IActionResult> Manage(string slug, Guid id, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            var session = await _context.Sessions
                .Include(s => s.Submissions).ThenInclude(sub => sub.Author)
                .FirstOrDefaultAsync(s => s.Id == id && s.ConferenceId == conference.Id);

            if (session == null) return NotFound();

            var unassignedSubmissions = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id
                            && (s.Status == SubmissionStatus.Accepted || s.Status == SubmissionStatus.Presented)
                            && s.SessionId == null)
                .Include(s => s.Author)
                .OrderBy(s => s.Title)
                .ToListAsync();

            ViewBag.UnassignedSubmissions = unassignedSubmissions;
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;

            return View(session);
        }

        [HttpPost("/{slug}/Admin/Session/AddSubmission")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubmission(string slug, Guid sessionId, Guid submissionId, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            var session = await _context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.ConferenceId == conference.Id);

            if (session == null) return NotFound();

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId && s.ConferenceId == conference.Id);

            if (submission != null)
            {
                submission.SessionId = sessionId;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Bildiri oturuma eklendi.";
            }

            return Redirect($"/{slug}/Admin/Session/Manage/{sessionId}?conferenceId={conference.Id}");
        }

        [HttpPost("/{slug}/Admin/Session/RemoveSubmission")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSubmission(string slug, Guid sessionId, Guid submissionId, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId && s.ConferenceId == conference.Id);

            if (submission != null && submission.SessionId == sessionId)
            {
                submission.SessionId = null;
                await _context.SaveChangesAsync();
                TempData["InfoMessage"] = "Bildiri oturumdan çıkarıldı.";
            }

            return Redirect($"/{slug}/Admin/Session/Manage/{sessionId}?conferenceId={conference.Id}");
        }

        [HttpPost("/{slug}/Admin/Session/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id, Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);
            if (conference == null) return Redirect("/Admin/Session");

            var session = await _context.Sessions
                .Include(s => s.Submissions)
                .FirstOrDefaultAsync(s => s.Id == id && s.ConferenceId == conference.Id);

            if (session != null)
            {
                foreach (var sub in session.Submissions)
                    sub.SessionId = null;

                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Oturum silindi.";
            }

            return Redirect($"/{slug}/Admin/Session?conferenceId={conference.Id}");
        }

    }
}
