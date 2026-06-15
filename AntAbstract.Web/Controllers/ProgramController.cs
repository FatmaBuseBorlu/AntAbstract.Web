using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<AppUser> _userManager;

        public ProgramController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
        }

        private static string GetCanonicalSlug(Conference conference, string fallbackSlug)
        {
            return conference.Tenant?.Slug
                   ?? conference.Slug
                   ?? fallbackSlug
                   ?? "";
        }

        private static bool SlugMatches(Conference conference, string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(conference.Slug) &&
                string.Equals(conference.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug) &&
                string.Equals(conference.Tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private Guid? GetSelectedConferenceIdFromSession(Guid? tenantId = null)
        {
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                var tenantSpecificValue = HttpContext.Session.GetString(
                    $"SelectedConferenceId:{tenantId.Value}");

                if (Guid.TryParse(tenantSpecificValue, out var tenantSpecificConferenceId) &&
                    tenantSpecificConferenceId != Guid.Empty)
                {
                    return tenantSpecificConferenceId;
                }
            }

            var globalValue = HttpContext.Session.GetString("SelectedConferenceId");

            if (Guid.TryParse(globalValue, out var globalConferenceId) &&
                globalConferenceId != Guid.Empty)
            {
                return globalConferenceId;
            }

            return null;
        }

        private async Task<Conference?> FindConferenceBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .Where(c =>
                    c.Slug == slug ||
                    (
                        c.Tenant != null &&
                        c.Tenant.Slug == slug
                    ))
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();
        }

        private async Task<Conference?> ResolveConferenceAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            Guid? selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                selectedConferenceId = GetSelectedConferenceIdFromSession(_tenantContext.Current?.Id);
            }

            if (selectedConferenceId.HasValue && selectedConferenceId.Value != Guid.Empty)
            {
                var selectedConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);

                if (selectedConference != null && SlugMatches(selectedConference, slug))
                {
                    return selectedConference;
                }
            }

            return await FindConferenceBySlugAsync(slug);
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

        private async Task<bool> CanUserTrackAttendanceAsync(
            string userId,
            Guid conferenceId)
        {
            var registration = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r =>
                    r.AppUserId == userId &&
                    r.ConferenceId == conferenceId);

            if (registration == null)
            {
                return false;
            }

            if (registration.IsPaid)
            {
                return true;
            }

            var amount = registration.RegistrationType?.Price ?? registration.Amount;

            return amount <= 0;
        }

        private async Task<ConferenceAttendance?> GetAttendanceAsync(
            string userId,
            Guid conferenceId)
        {
            return await _context.ConferenceAttendances
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.ConferenceId == conferenceId);
        }

        private async Task FillProgramViewBagAsync(
            Conference conference,
            string slug)
        {
            ViewBag.ConferenceName = conference.Title;
            ViewBag.Slug = slug;
            ViewBag.ConferenceId = conference.Id;

            ViewBag.AttendanceJoinUrl = $"/{slug}/Attendance/Join";
            ViewBag.AttendancePingUrl = $"/{slug}/Attendance/Ping";
            ViewBag.CanTrackAttendance = false;
            ViewBag.AttendanceTotalSeconds = 0;
            ViewBag.AttendanceRequiredSeconds = 600;
            ViewBag.AttendanceCompleted = false;

            if (User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return;
            }

            var canTrackAttendance = await CanUserTrackAttendanceAsync(
                user.Id,
                conference.Id);

            ViewBag.CanTrackAttendance = canTrackAttendance;

            var attendance = await GetAttendanceAsync(
                user.Id,
                conference.Id);

            if (attendance != null)
            {
                ViewBag.AttendanceTotalSeconds = attendance.TotalSeconds;
                ViewBag.AttendanceRequiredSeconds = attendance.RequiredSeconds;
                ViewBag.AttendanceCompleted = attendance.CompletedAt.HasValue ||
                                              attendance.TotalSeconds >= attendance.RequiredSeconds;
            }
        }

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

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect($"/{canonicalSlug}/Program/Index");
            }

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

            await FillProgramViewBagAsync(conference, canonicalSlug);

            return View(sessions);
        }

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

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect($"/{canonicalSlug}/Program/Details/{id}");
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

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

            await FillProgramViewBagAsync(conference, canonicalSlug);

            return View(session);
        }
    }
}