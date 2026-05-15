using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private const int DefaultRequiredSeconds = 600;
        private const int MaxPingDeltaSeconds = 120;

        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICertificateService _certificateService;

        public AttendanceController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            ICertificateService certificateService)
        {
            _context = context;
            _userManager = userManager;
            _certificateService = certificateService;
        }

        private async Task<Conference?> GetValidConferenceAsync(string slug, Guid conferenceId)
        {
            if (string.IsNullOrWhiteSpace(slug) || conferenceId == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Id == conferenceId &&
                    c.Tenant != null &&
                    c.Tenant.Slug == slug);
        }

        private async Task<bool> CanUserAttendConferenceAsync(string userId, Guid conferenceId)
        {
            var registration = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == conferenceId &&
                    r.AppUserId == userId);

            if (registration == null)
            {
                return false;
            }

            /*
             * Eğer katılım için ödeme zorunluysa bu kontrol kalsın.
             * Eğer ücretsiz kongrede de katılım olacaksa bu kısmı kongrenin ücret/kayıt tipine göre ayrıca esnetebiliriz.
             */
            if (!registration.IsPaid)
            {
                return false;
            }

            return true;
        }

        private async Task<ConferenceAttendance> GetOrCreateAttendanceAsync(Guid conferenceId, string userId)
        {
            var attendance = await _context.ConferenceAttendances
                .FirstOrDefaultAsync(x =>
                    x.ConferenceId == conferenceId &&
                    x.UserId == userId);

            var now = DateTime.UtcNow;

            if (attendance != null)
            {
                return attendance;
            }

            attendance = new ConferenceAttendance
            {
                ConferenceId = conferenceId,
                UserId = userId,
                FirstJoinedAt = now,
                LastPingAt = now,
                TotalSeconds = 0,
                RequiredSeconds = DefaultRequiredSeconds,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].ToString()
            };

            _context.ConferenceAttendances.Add(attendance);

            return attendance;
        }

        [HttpPost("/{slug}/Attendance/Join")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(string slug, Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var conference = await GetValidConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Geçersiz kongre."
                });
            }

            var canAttend = await CanUserAttendConferenceAsync(user.Id, conference.Id);

            if (!canAttend)
            {
                return Forbid();
            }

            var attendance = await GetOrCreateAttendanceAsync(conference.Id, user.Id);

            attendance.LastPingAt = DateTime.UtcNow;
            attendance.UserAgent = Request.Headers["User-Agent"].ToString();

            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                totalSeconds = attendance.TotalSeconds,
                requiredSeconds = attendance.RequiredSeconds,
                completed = attendance.IsCompleted
            });
        }

        [HttpPost("/{slug}/Attendance/Ping")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ping(string slug, Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var conference = await GetValidConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Geçersiz kongre."
                });
            }

            var canAttend = await CanUserAttendConferenceAsync(user.Id, conference.Id);

            if (!canAttend)
            {
                return Forbid();
            }

            var attendance = await GetOrCreateAttendanceAsync(conference.Id, user.Id);

            var now = DateTime.UtcNow;
            var lastPing = attendance.LastPingAt ?? now;

            var delta = (int)Math.Floor((now - lastPing).TotalSeconds);

            if (delta < 0)
            {
                delta = 0;
            }

            if (delta > MaxPingDeltaSeconds)
            {
                delta = MaxPingDeltaSeconds;
            }

            if (!attendance.CompletedAt.HasValue)
            {
                attendance.TotalSeconds += delta;

                if (attendance.TotalSeconds >= attendance.RequiredSeconds)
                {
                    attendance.CompletedAt = now;
                }
            }

            attendance.LastPingAt = now;
            attendance.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            attendance.UserAgent = Request.Headers["User-Agent"].ToString();

            await _context.SaveChangesAsync();

            if (attendance.CompletedAt.HasValue)
            {
                await _certificateService.EnsureAuthorCertificateAsync(conference.Id, user.Id);
            }

            return Json(new
            {
                ok = true,
                totalSeconds = attendance.TotalSeconds,
                requiredSeconds = attendance.RequiredSeconds,
                completed = attendance.IsCompleted
            });
        }
    }
}