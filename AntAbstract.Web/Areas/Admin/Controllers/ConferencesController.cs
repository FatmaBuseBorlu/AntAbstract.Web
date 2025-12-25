using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ConferencesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ConferencesController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        [HttpGet("/{slug}/admin/conferences")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null) return NotFound();

            var conferences = await _context.Conferences
                .Where(c => c.TenantId == _tenantContext.Current.Id)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(conferences);
        }

        [HttpGet("/{slug}/admin/conferences/create")]
        public IActionResult Create(string slug)
        {
            return View();
        }

        [HttpPost("/{slug}/admin/conferences/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string slug, Conference conference)
        {
            if (_tenantContext.Current == null) return NotFound();

            if (ModelState.IsValid || true)
            {
                conference.Id = Guid.NewGuid();
                conference.TenantId = _tenantContext.Current.Id;

                _context.Conferences.Add(conference);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Kongre başarıyla oluşturuldu.";
                return Redirect($"/{slug}/admin/conferences");
            }
            return View(conference);
        }

        [HttpGet("/{slug}/admin/conferences/edit/{id}")]
        public async Task<IActionResult> Edit(string slug, Guid id)
        {
            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return NotFound();

            return View(conference);
        }

        [HttpPost("/{slug}/admin/conferences/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string slug, Guid id, Conference conference)
        {
            if (id != conference.Id) return NotFound();

            var existingConf = await _context.Conferences.FindAsync(id);
            if (existingConf == null) return NotFound();

            existingConf.Title = conference.Title;
            existingConf.StartDate = conference.StartDate;
            existingConf.EndDate = conference.EndDate;
            existingConf.Description = conference.Description;
            existingConf.Venue = conference.Venue;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Kongre bilgileri güncellendi.";

            return Redirect($"/{slug}/admin/conferences");
        }

        [HttpPost("/{slug}/admin/conferences/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string slug, Guid id)
        {
            var conference = await _context.Conferences.FindAsync(id);
            if (conference != null)
            {
                _context.Conferences.Remove(conference);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Kongre silindi.";
            }
            return Redirect($"/{slug}/admin/conferences");
        }
    }
}