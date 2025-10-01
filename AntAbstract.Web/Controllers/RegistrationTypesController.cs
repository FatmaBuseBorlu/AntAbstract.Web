using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;

namespace AntAbstract.Web.Controllers
{
    public class RegistrationTypesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public RegistrationTypesController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: RegistrationTypes
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.RegistrationTypes.Include(r => r.Conference);
            return View(await appDbContext.ToListAsync());
        }

        // GET: RegistrationTypes/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var registrationType = await _context.RegistrationTypes
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (registrationType == null)
            {
                return NotFound();
            }

            return View(registrationType);
        }

        // Controllers/RegistrationTypesController.cs içine

        // GET: RegistrationTypes/Create - GÜNCELLENDİ
        public async Task<IActionResult> Create()
        {
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return RedirectToAction(nameof(Index));

            var registrationType = new RegistrationType { ConferenceId = conference.Id };
            return View(registrationType);
        }

        // POST: RegistrationTypes/Create - GÜNCELLENDİ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Price,Currency,ConferenceId")] RegistrationType registrationType)
        {
            if (ModelState.IsValid)
            {
                registrationType.Id = Guid.NewGuid();
                _context.Add(registrationType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(registrationType);
        }

        // GET: RegistrationTypes/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var registrationType = await _context.RegistrationTypes.FindAsync(id);
            if (registrationType == null)
            {
                return NotFound();
            }
            ViewData["ConferenceId"] = new SelectList(_context.Conferences, "Id", "Title", registrationType.ConferenceId);
            return View(registrationType);
        }

        // POST: RegistrationTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Description,Price,Currency,ConferenceId")] RegistrationType registrationType)
        {
            if (id != registrationType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(registrationType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RegistrationTypeExists(registrationType.Id))
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
            ViewData["ConferenceId"] = new SelectList(_context.Conferences, "Id", "Title", registrationType.ConferenceId);
            return View(registrationType);
        }

        // GET: RegistrationTypes/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var registrationType = await _context.RegistrationTypes
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (registrationType == null)
            {
                return NotFound();
            }

            return View(registrationType);
        }

        // POST: RegistrationTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var registrationType = await _context.RegistrationTypes.FindAsync(id);
            if (registrationType != null)
            {
                _context.RegistrationTypes.Remove(registrationType);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RegistrationTypeExists(Guid id)
        {
            return _context.RegistrationTypes.Any(e => e.Id == id);
        }
    }
}
