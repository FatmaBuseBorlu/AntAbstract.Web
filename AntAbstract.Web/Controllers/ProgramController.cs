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
    public class ProgramController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public ProgramController(
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
            Guid? selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                var selectedIdText = HttpContext.Session.GetString("SelectedConferenceId");

                if (Guid.TryParse(selectedIdText, out var sessionConferenceId))
                {
                    selectedConferenceId = sessionConferenceId;
                }
            }

            if (selectedConferenceId.HasValue && selectedConferenceId.Value != Guid.Empty)
            {
                var selectedConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.Id == selectedConferenceId.Value &&
                        (
                            c.Slug == slug ||
                            (c.Tenant != null && c.Tenant.Slug == slug)
                        ));

                if (selectedConference != null)
                {
                    return selectedConference;
                }
            }

            return await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .Where(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug))
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();
        }

        private void SetSelectedConferenceSession(Conference conference, string slug)
        {
            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            if (conference.TenantId != Guid.Empty)
            {
                HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
                HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
                HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
            }
        }

        [HttpGet("/{slug}/program")]
        [HttpGet("/{slug}/Program")]
        [HttpGet("/{slug}/Program/Index")]
        public async Task<IActionResult> Index(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return NotFound();
            }

            var conference = await ResolveConferenceAsync(slug);

            if (conference == null)
            {
                return NotFound();
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            var sessions = await _context.Sessions
                .AsNoTracking()
                .Where(s =>
                    s.ConferenceId == conference.Id &&
                    s.IsActive)
                .Include(s => s.Submissions)
                    .ThenInclude(sub => sub.Author)
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.StartTime)
                .ThenBy(s => s.SortOrder)
                .ToListAsync();

            ViewBag.ConferenceName = conference.Title;
            ViewBag.Slug = canonicalSlug;
            ViewBag.ConferenceId = conference.Id;

            return View(sessions);
        }

        [HttpGet("/{slug}/program/details/{id:guid}")]
        [HttpGet("/{slug}/Program/Details/{id:guid}")]
        public async Task<IActionResult> Details(string slug, Guid id)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return NotFound();
            }

            var conference = await ResolveConferenceAsync(slug);

            if (conference == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .AsNoTracking()
                .Include(s => s.Submissions)
                    .ThenInclude(sub => sub.Author)
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.ConferenceId == conference.Id &&
                    s.IsActive);

            if (session == null)
            {
                return NotFound();
            }

            ViewBag.ConferenceName = conference.Title;
            ViewBag.Slug = conference.Tenant?.Slug ?? conference.Slug ?? slug;
            ViewBag.ConferenceId = conference.Id;

            return View(session);
        }
    }
}