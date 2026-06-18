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
    public class SpeakersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public SpeakersController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        private async Task<Conference?> ResolveConferenceAsync(string slug)
        {
            var confIdStr = HttpContext.Session.GetString("SelectedConferenceId");
            if (Guid.TryParse(confIdStr, out var confId) && confId != Guid.Empty)
            {
                var bySession = await _context.Conferences.Include(c => c.Tenant)
                    .AsNoTracking().FirstOrDefaultAsync(c => c.Id == confId);
                if (bySession != null && (bySession.Slug == slug || bySession.Tenant?.Slug == slug))
                    return bySession;
            }

            return await _context.Conferences.Include(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == slug || (c.Tenant != null && c.Tenant.Slug == slug));
        }

        [HttpGet("/{slug}/Speakers")]
        [HttpGet("/{slug}/Speakers/Index")]
        public async Task<IActionResult> Index(string slug)
        {
            var conference = await ResolveConferenceAsync(slug);
            if (conference == null) return NotFound();

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);
            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", canonicalSlug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            var speakers = await _context.InvitedSpeakers
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id && s.IsActive)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.FullName)
                .ToListAsync();

            ViewBag.Slug = canonicalSlug;
            ViewBag.ConferenceTitle = conference.Title;

            return View(speakers);
        }
    }
}
