using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("api")]
    public class ConferencesApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConferencesApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool? active = null)
        {
            var query = _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (active == true)
                query = query.Where(c => c.EndDate >= DateTime.UtcNow);
            else if (active == false)
                query = query.Where(c => c.EndDate < DateTime.UtcNow);

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    slug = c.Tenant != null ? c.Tenant.Slug : c.Slug,
                    c.StartDate,
                    c.EndDate,
                    location = c.City,
                    c.Country,
                    isSubmissionOpen = c.IsSubmissionOpen,
                    isRegistrationOpen = c.IsRegistrationOpen,
                    institution = c.Tenant != null ? c.Tenant.Name : null
                })
                .ToListAsync();

            return Ok(new { count = conferences.Count, data = conferences });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Detail(Guid id)
        {
            var c = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return NotFound(new { error = "Kongre bulunamadı." });

            var topics = await _context.ConferenceTopics
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.ConferenceId == id)
                .Select(t => new { t.Id, t.Name, t.NameEn })
                .ToListAsync();

            return Ok(new
            {
                c.Id,
                c.Title,
                c.Description,
                slug = c.Tenant?.Slug ?? c.Slug,
                c.StartDate,
                c.EndDate,
                location = c.City,
                c.Country,
                c.IsSubmissionOpen,
                c.IsRegistrationOpen,
                c.IsBlindReview,
                institution = c.Tenant?.Name,
                topics
            });
        }

        [HttpGet("{id:guid}/program")]
        public async Task<IActionResult> Program(Guid id)
        {
            var sessions = await _context.Sessions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.ConferenceId == id && s.IsActive)
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.StartTime)
                .ThenBy(s => s.SortOrder)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    s.TitleEn,
                    date = s.SessionDate.ToString("yyyy-MM-dd"),
                    startTime = s.StartTime.ToString(@"hh\:mm"),
                    endTime = s.EndTime.ToString(@"hh\:mm"),
                    s.Location,
                    s.SpeakerName,
                    s.PresentationTitle,
                    submissionCount = s.Submissions.Count
                })
                .ToListAsync();

            return Ok(new { conferenceId = id, count = sessions.Count, data = sessions });
        }

        [HttpGet("{id:guid}/stats")]
        public async Task<IActionResult> Stats(Guid id)
        {
            var conf = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conf == null) return NotFound();

            var submissions = await _context.Submissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.ConferenceId == id)
                .GroupBy(s => s.Status)
                .Select(g => new { status = g.Key.ToString(), count = g.Count() })
                .ToListAsync();

            var registrations = await _context.Registrations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.ConferenceId == id)
                .CountAsync();

            return Ok(new
            {
                conferenceId = id,
                totalRegistrations = registrations,
                submissions
            });
        }
    }
}
