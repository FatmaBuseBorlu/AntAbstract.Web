using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AntAbstract.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class SubmissionsApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubmissionsApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> MySubmissions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var submissions = await _context.Submissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(s => s.Conference)
                .Where(s => s.AuthorId == userId)
                .OrderByDescending(s => s.CreatedDate)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    status = s.Status.ToString(),
                    s.Keywords,
                    s.CreatedDate,
                    s.DecisionDate,
                    s.DoiUrl,
                    conference = new { s.Conference.Id, s.Conference.Title },
                    hasRebuttal = !string.IsNullOrWhiteSpace(s.RebuttalText),
                    fileCount = s.Files.Count
                })
                .ToListAsync();

            return Ok(new { count = submissions.Count, data = submissions });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Detail(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var submission = await _context.Submissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(s => s.Conference)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return NotFound(new { error = "Bildiri bulunamadı." });

            var isOwner = submission.AuthorId == userId;
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

            if (!isOwner && !isAdmin)
                return Forbid();

            return Ok(new
            {
                submission.Id,
                submission.Title,
                submission.Abstract,
                submission.Keywords,
                submission.Topic,
                submission.PresentationType,
                status = submission.Status.ToString(),
                submission.CreatedDate,
                submission.DecisionDate,
                submission.DoiUrl,
                submission.AdminDecisionNote,
                submission.RebuttalText,
                submission.RebuttalDate,
                conference = new { submission.Conference.Id, submission.Conference.Title },
                authors = submission.SubmissionAuthors?.Select(a => new
                {
                    a.FirstName,
                    a.LastName,
                    a.Email,
                    a.Institution
                }),
                files = submission.Files?.Select(f => new
                {
                    f.Id,
                    f.FileName,
                    type = f.Type.ToString(),
                    f.Version,
                    f.UploadedAt,
                    downloadUrl = $"/download/submission/{f.Id}"
                })
            });
        }

        [HttpGet("conference/{conferenceId:guid}")]
        [Authorize(AuthenticationSchemes = "Bearer", Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> ByConference(
            Guid conferenceId,
            [FromQuery] string? status = null)
        {
            var query = _context.Submissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == conferenceId);

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<SubmissionStatus>(status, true, out var parsed))
            {
                query = query.Where(s => s.Status == parsed);
            }

            var submissions = await query
                .OrderByDescending(s => s.CreatedDate)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    status = s.Status.ToString(),
                    s.Keywords,
                    s.CreatedDate,
                    s.DecisionDate,
                    s.DoiUrl,
                    author = s.Author != null ? $"{s.Author.FirstName} {s.Author.LastName}" : null,
                    authorEmail = s.Author != null ? s.Author.Email : null
                })
                .ToListAsync();

            return Ok(new { conferenceId, count = submissions.Count, data = submissions });
        }
    }
}
