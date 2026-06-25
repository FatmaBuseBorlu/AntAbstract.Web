using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
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
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("api")]
    public class SubmissionsApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAdminTenantAccessService _tenantAccess;

        public SubmissionsApiController(
            AppDbContext context,
            IAdminTenantAccessService tenantAccess)
        {
            _context = context;
            _tenantAccess = tenantAccess;
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
                    doiStatus = s.DoiStatus.ToString(),
                    s.DoiProvider,
                    s.DoiAssignedAt,
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
            var isAdmin = await CanAccessSubmissionAsAdminAsync(submission);

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
                doiStatus = submission.DoiStatus.ToString(),
                submission.DoiProvider,
                submission.DoiErrorMessage,
                submission.DoiRequestedAt,
                submission.DoiAssignedAt,
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
            var canAccessConference = await CanAccessConferenceAsync(conferenceId);
            if (!canAccessConference.HasValue)
                return NotFound(new { error = "Kongre bulunamadı." });

            if (!canAccessConference.Value)
                return Forbid();

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
                    doiStatus = s.DoiStatus.ToString(),
                    s.DoiProvider,
                    s.DoiAssignedAt,
                    author = s.Author != null ? $"{s.Author.FirstName} {s.Author.LastName}" : null,
                    authorEmail = s.Author != null ? s.Author.Email : null
                })
                .ToListAsync();

            return Ok(new { conferenceId, count = submissions.Count, data = submissions });
        }

        private async Task<bool> CanAccessSubmissionAsAdminAsync(Submission submission)
        {
            if (_tenantAccess.IsSuperAdmin(User))
                return true;

            if (!User.IsInRole("Admin"))
                return false;

            var adminTenantId = await _tenantAccess.GetAdminTenantIdAsync(User);
            return adminTenantId.HasValue && submission.TenantId == adminTenantId.Value;
        }

        private async Task<bool?> CanAccessConferenceAsync(Guid conferenceId)
        {
            var tenantId = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == conferenceId)
                .Select(c => (Guid?)c.TenantId)
                .FirstOrDefaultAsync();

            if (!tenantId.HasValue)
                return null;

            if (_tenantAccess.IsSuperAdmin(User))
                return true;

            if (!User.IsInRole("Admin"))
                return false;

            var adminTenantId = await _tenantAccess.GetAdminTenantIdAsync(User);
            return adminTenantId.HasValue && adminTenantId.Value == tenantId.Value;
        }
    }
}
