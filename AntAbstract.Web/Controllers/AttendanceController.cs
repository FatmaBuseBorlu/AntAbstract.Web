using AntAbstract.Application.Interfaces;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICertificateService _certificateService;

        public AttendanceController(AppDbContext context, UserManager<AppUser> userManager, ICertificateService certificateService)
        {
            _context = context;
            _userManager = userManager;
            _certificateService = certificateService;
        }

        [HttpPost("/{slug}/Attendance/Join")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(string slug, Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var att = await _context.ConferenceAttendances
                .FirstOrDefaultAsync(x => x.ConferenceId == conferenceId && x.UserId == user.Id);

            var now = DateTime.UtcNow;

            if (att == null)
            {
                att = new ConferenceAttendance
                {
                    ConferenceId = conferenceId,
                    UserId = user.Id,
                    FirstJoinedAt = now,
                    LastPingAt = now,
                    TotalSeconds = 0,
                    RequiredSeconds = 600,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                };

                _context.ConferenceAttendances.Add(att);
            }
            else
            {
                att.LastPingAt = now;
                att.UserAgent = Request.Headers.UserAgent.ToString();
            }

            await _context.SaveChangesAsync();

            return Json(new { ok = true });
        }

        [HttpPost("/{slug}/Attendance/Ping")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ping(string slug, Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var att = await _context.ConferenceAttendances
                .FirstOrDefaultAsync(x => x.ConferenceId == conferenceId && x.UserId == user.Id);

            var now = DateTime.UtcNow;

            if (att == null)
            {
                att = new ConferenceAttendance
                {
                    ConferenceId = conferenceId,
                    UserId = user.Id,
                    FirstJoinedAt = now,
                    LastPingAt = now,
                    TotalSeconds = 0,
                    RequiredSeconds = 600,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                };
                _context.ConferenceAttendances.Add(att);
                await _context.SaveChangesAsync();

                return Json(new { totalSeconds = att.TotalSeconds, requiredSeconds = att.RequiredSeconds, completed = att.IsCompleted });
            }

            var last = att.LastPingAt ?? now;
            var delta = (int)Math.Floor((now - last).TotalSeconds);

            if (delta < 0) delta = 0;
            if (delta > 120) delta = 120;

            att.TotalSeconds += delta;
            att.LastPingAt = now;

            if (!att.CompletedAt.HasValue && att.TotalSeconds >= att.RequiredSeconds)
            {
                att.CompletedAt = now;
            }

            await _context.SaveChangesAsync();

            if (att.CompletedAt.HasValue)
            {
                await _certificateService.EnsureAuthorCertificateAsync(conferenceId, user.Id);
            }

            return Json(new { totalSeconds = att.TotalSeconds, requiredSeconds = att.RequiredSeconds, completed = att.IsCompleted });
        }
    }
}
