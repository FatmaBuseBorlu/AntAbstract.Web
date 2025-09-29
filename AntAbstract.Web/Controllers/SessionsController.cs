using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class SessionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public SessionsController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: Sessions
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null) return RedirectToAction("Index", "Home");

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Bu kongre için henüz bir etkinlik (conference) tanımlanmamış. Oturum ekleyebilmek için önce etkinlik oluşturulmalıdır.";
                return View(new List<Session>());
            }

            var sessions = await _context.Sessions
                .Where(s => s.ConferenceId == conference.Id)
                .OrderBy(s => s.SessionDate)
                .ToListAsync();

            return View(sessions);
        }

        // GET: Sessions/Create
        public async Task<IActionResult> Create()
        {
            if (_tenantContext.Current == null) return RedirectToAction("Index", "Home");

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return RedirectToAction(nameof(Index));

            var session = new Session { ConferenceId = conference.Id };
            return View(session);
        }

        // POST: Sessions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,SessionDate,Location,ConferenceId")] Session session)
        {
            if (ModelState.IsValid)
            {
                session.Id = Guid.NewGuid();
                _context.Add(session);
                await _context.SaveChangesAsync();

                var tenantSlug = _tenantContext.Current?.Slug;
                return RedirectToAction(nameof(Index), new { tenant = tenantSlug });
            }
            return View(session);
        }

        // GET: Sessions/Manage/5
        public async Task<IActionResult> Manage(Guid id)
        {
            var session = await _context.Sessions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null) return NotFound();

            var assignedSubmissions = await _context.Submissions
                .Include(s => s.Author)
                .Where(s => s.SessionId == id)
                .OrderBy(s => s.Title)
                .ToListAsync();

            // ✅ ÖNEMLİ DÜZELTME: Karar alanı "FinalDecision" ve değeri "Kabul Edildi" olarak güncellendi.
            var availableSubmissions = await _context.Submissions
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == session.ConferenceId &&
                            s.FinalDecision == "Kabul Edildi" &&
                            s.SessionId == null)
                .OrderBy(s => s.Title)
                .ToListAsync();

            var viewModel = new SessionManageViewModel
            {
                Session = session,
                AssignedSubmissions = assignedSubmissions,
                AvailableSubmissions = availableSubmissions
            };

            return View("Manage", viewModel);
        }

        // POST: Sessions/AddToSession
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToSession(Guid sessionId, Guid submissionId)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission != null)
            {
                submission.SessionId = sessionId;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Manage), new { id = sessionId });
        }

        // POST: Sessions/RemoveFromSession
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromSession(Guid sessionId, Guid submissionId)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission != null)
            {
                submission.SessionId = null;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Manage), new { id = sessionId });
        }

        // GET: Sessions/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var session = await _context.Sessions.FindAsync(id);
            if (session == null) return NotFound();
            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,SessionDate,Location,ConferenceId")] Session session)
        {
            if (id != session.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(session);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SessionExists(session.Id)) return NotFound();
                    else throw;
                }
                var tenantSlug = _tenantContext.Current?.Slug;
                return RedirectToAction(nameof(Index), new { tenant = tenantSlug });
            }
            return View(session);
        }

        // GET: Sessions/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var session = await _context.Sessions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (session == null) return NotFound();

            return View(session);
        }

        // POST: Sessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session != null)
            {
                _context.Sessions.Remove(session);
            }
            await _context.SaveChangesAsync();

            var tenantSlug = _tenantContext.Current?.Slug;
            return RedirectToAction(nameof(Index), new { tenant = tenantSlug });
        }

        private bool SessionExists(Guid id)
        {
            return _context.Sessions.Any(e => e.Id == id);
        }
    }
}