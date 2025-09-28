using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

            var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
            if (conference == null) return View(new List<Session>());

            var sessions = await _context.Sessions
                .Where(s => s.ConferenceId == conference.Id)
                .ToListAsync();
            return View(sessions);
        }

        // GET: Sessions/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (session == null)
            {
                return NotFound();
            }

            return View(session);
        }

        // GET: Sessions/Create
        public async Task<IActionResult> Create()
        {
            if (_tenantContext.Current == null) return RedirectToAction("Index", "Home");

            // O anki kongreye ait Konferans'ı bul ve ID'sini View'a gönder
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                // Hata yönetimi
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ConferenceId = conference.Id;
            return View();
        }

        // POST: Sessions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection collection)
        {
            try
            {
                Guid conferenceId = Guid.Parse(collection["ConferenceId"]);
                var session = new Session
                {
                    Title = collection["Title"],
                    SessionDate = DateTime.Parse(collection["SessionDate"]),
                    Location = collection["Location"],
                    ConferenceId = conferenceId
                };

                _context.Add(session);
                await _context.SaveChangesAsync();

                var tenantSlug = _tenantContext.Current?.Slug;
                return RedirectToAction(nameof(Index), new { tenant = tenantSlug });
            }
            catch
            {
                return View();
            }
        }

        // GET: Sessions/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions.FindAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            ViewData["ConferenceId"] = new SelectList(_context.Conferences, "Id", "Title", session.ConferenceId);
            return View(session);
        }

        // POST: Sessions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,SessionDate,Location,ConferenceId")] Session session)
        {
            if (id != session.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(session);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SessionExists(session.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["ConferenceId"] = new SelectList(_context.Conferences, "Id", "Title", session.ConferenceId);
            return View(session);
        }

        // GET: Sessions/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (session == null)
            {
                return NotFound();
            }

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
            return RedirectToAction(nameof(Index));
        }

        private bool SessionExists(Guid id)
        {
            return _context.Sessions.Any(e => e.Id == id);
        }
        // GET: Sessions/Manage/5
        // Bir oturumun detaylarını ve o oturuma atanmış/atanabilecek özetleri gösterir.
        public async Task<IActionResult> Manage(Guid? id)
        {
            if (id == null) return NotFound();

            var session = await _context.Sessions
                .Include(s => s.Submissions) // Oturuma atanmış özetleri yükle
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null) return NotFound();

            // Bu konferansta "Kabul Edildi" durumunda olan VE
            // henüz bu oturuma atanmamış olan diğer özetleri bul.
            var availableSubmissions = await _context.Submissions
                .Where(s => s.ConferenceId == session.ConferenceId &&
                             s.FinalDecision == "Kabul Edildi" &&
                             s.SessionId != session.Id)
                .ToListAsync();

            var viewModel = new SessionDetailViewModel
            {
                Session = session,
                AvailableSubmissions = new SelectList(availableSubmissions, "SubmissionId", "Title")
            };

            return View(viewModel);
        }

        // POST: Sessions/AssignSubmission
        // Bir özeti bir oturuma atar.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignSubmission(Guid sessionId, Guid submissionIdToAdd)
        {
            var submission = await _context.Submissions.FindAsync(submissionIdToAdd);
            var session = await _context.Sessions.FindAsync(sessionId);

            if (submission == null || session == null)
            {
                return NotFound();
            }

            // Özeti oturuma ata
            submission.SessionId = sessionId;
            await _context.SaveChangesAsync();

            // Kullanıcıyı tekrar yönetim sayfasına yönlendir.
            return RedirectToAction(nameof(Manage), new { id = sessionId });
        }
    }
}
