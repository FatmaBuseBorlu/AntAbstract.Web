using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
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

        public SessionController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        [HttpGet("/{slug}/admin/session")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null) return Redirect("/admin/assignment");

            var sessions = await _context.Sessions
                .Where(s => s.ConferenceId == _tenantContext.Current.Id)
                .Include(s => s.Submissions) 
                .OrderBy(s => s.SessionDate)
                .ToListAsync();

            ViewBag.ConferenceName = _tenantContext.Current.Name;
            return View(sessions);
        }

        [HttpGet("/{slug}/admin/session/create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost("/{slug}/admin/session/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Session session)
        {
            if (_tenantContext.Current == null) return NotFound();

            if (ModelState.IsValid)
            {
                session.Id = Guid.NewGuid();
                session.ConferenceId = _tenantContext.Current.Id;

                _context.Sessions.Add(session);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Oturum başarıyla oluşturuldu.";
                return Redirect($"/{slug}/admin/session");
            }
            return View(session);
        }

        [HttpGet("/{slug}/admin/session/edit/{id}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null) return NotFound();
            return View(session);
        }

        [HttpPost("/{slug}/admin/session/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string slug, Guid id, Session session)
        {
            if (id != session.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSession = await _context.Sessions.FindAsync(id);
                    existingSession.Title = session.Title;
                    existingSession.Location = session.Location;
                    existingSession.SessionDate = session.SessionDate;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Oturum güncellendi.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Sessions.AnyAsync(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return Redirect($"/{slug}/admin/session");
            }
            return View(session);
        }

        [HttpGet("/{slug}/admin/session/manage/{id}")]
        public async Task<IActionResult> Manage(string slug, Guid id)
        {
            var session = await _context.Sessions
                .Include(s => s.Submissions).ThenInclude(sub => sub.Author)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null) return NotFound();

            var unassignedSubmissions = await _context.Submissions
                .Where(s => s.ConferenceId == _tenantContext.Current.Id
                            && (s.Status == SubmissionStatus.Accepted || s.Status == SubmissionStatus.Presented)
                            && s.SessionId == null)
                .Include(s => s.Author)
                .OrderBy(s => s.Title)
                .ToListAsync();

            ViewBag.UnassignedSubmissions = unassignedSubmissions;

            return View(session);
        }

        [HttpPost("/{slug}/admin/session/addsubmission")]
        public async Task<IActionResult> AddSubmission(string slug, Guid sessionId, Guid submissionId)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission != null)
            {
                submission.SessionId = sessionId;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Bildiri oturuma eklendi.";
            }
            return Redirect($"/{slug}/admin/session/manage/{sessionId}");
        }

        [HttpPost("/{slug}/admin/session/removesubmission")]
        public async Task<IActionResult> RemoveSubmission(string slug, Guid sessionId, Guid submissionId)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission != null && submission.SessionId == sessionId)
            {
                submission.SessionId = null; 
                await _context.SaveChangesAsync();
                TempData["InfoMessage"] = "Bildiri oturumdan çıkarıldı.";
            }
            return Redirect($"/{slug}/admin/session/manage/{sessionId}");
        }

        [HttpPost("/{slug}/admin/session/delete")]
        public async Task<IActionResult> Delete(string slug, Guid id)
        {
            var session = await _context.Sessions.Include(s => s.Submissions).FirstOrDefaultAsync(s => s.Id == id);
            if (session != null)
            {
                foreach (var sub in session.Submissions)
                {
                    sub.SessionId = null;
                }
                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Oturum silindi.";
            }
            return Redirect($"/{slug}/admin/session");
        }
    }
}