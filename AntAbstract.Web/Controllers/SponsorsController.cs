using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    public class SponsorsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public SponsorsController(AppDbContext context, ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _selectedConferenceService = selectedConferenceService;
        }

        private async Task<Conference?> ResolveConferenceAsync(string slug)
        {
            var confIdStr = HttpContext.Session.GetString("SelectedConferenceId");
            if (Guid.TryParse(confIdStr, out var confId) && confId != Guid.Empty)
            {
                var c = await _context.Conferences.IgnoreQueryFilters().Include(x => x.Tenant).AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == confId);
                if (c != null && (c.Slug == slug || c.Tenant?.Slug == slug)) return c;
            }
            return await _context.Conferences.IgnoreQueryFilters().Include(x => x.Tenant).AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == slug || (x.Tenant != null && x.Tenant.Slug == slug));
        }

        [HttpGet("/{slug}/Sponsors")]
        [HttpGet("/{slug}/Sponsors/Index")]
        public async Task<IActionResult> Index(string slug)
        {
            var conference = await ResolveConferenceAsync(slug);
            if (conference == null) return NotFound();

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;
            _selectedConferenceService.SetSelectedConferenceId(conference.Id);
            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", canonicalSlug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            var sponsors = await _context.Sponsors.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id && s.IsActive)
                .OrderBy(s => s.Tier).ThenBy(s => s.SortOrder).ThenBy(s => s.Name)
                .ToListAsync();

            ViewBag.Slug = canonicalSlug;
            ViewBag.ConferenceTitle = conference.Title;
            return View(sponsors);
        }
    }
}
