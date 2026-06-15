using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Threading.Tasks;

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
        private readonly IStringLocalizer<AttendanceController> _localizer;

        public AttendanceController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            ICertificateService certificateService,
            IStringLocalizer<AttendanceController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _certificateService = certificateService;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
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

        private void SetSelectedConferenceSession(Conference conference, string slug)
        {
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

        private async Task<Conference?> GetValidConferenceAsync(string slug, Guid conferenceId)
        {
            if (string.IsNullOrWhiteSpace(slug) || conferenceId == Guid.Empty)
            {
                return null;
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
            {
                return null;
            }

            return SlugMatches(conference, slug)
                ? conference
                : null;
        }

        private async Task<(bool CanAttend, string Message)> CanUserAttendConferenceAsync(
            string userId,
            Guid conferenceId)
        {
            var registration = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == conferenceId &&
                    r.AppUserId == userId);

            if (registration == null)
            {
                return (
                    false,
                    T("AttendanceRequiresRegistration", "Katılım takibi için önce bu kongreye kayıt olmalısınız.")
                );
            }

            if (registration.IsPaid)
            {
                return (true, "");
            }

            var amount = registration.RegistrationType?.Price ?? registration.Amount;

            if (amount <= 0)
            {
                return (true, "");
            }

            return (
                false,
                T("AttendanceRequiresPayment", "Katılım takibi için önce ödeme işlemini tamamlamalısınız.")
            );
        }

        private async Task<ConferenceAttendance> GetOrCreateAttendanceAsync(
            Guid conferenceId,
            string userId)
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
                return Unauthorized(new
                {
                    ok = false,
                    message = T("Unauthorized", "Oturum bulunamadı.")
                });
            }

            var conference = await GetValidConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = T("InvalidConference", "Geçersiz kongre.")
                });
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference, canonicalSlug);

            var access = await CanUserAttendConferenceAsync(user.Id, conference.Id);

            if (!access.CanAttend)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = access.Message
                });
            }

            var attendance = await GetOrCreateAttendanceAsync(conference.Id, user.Id);

            attendance.LastPingAt = DateTime.UtcNow;
            attendance.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            attendance.UserAgent = Request.Headers["User-Agent"].ToString();

            await _context.SaveChangesAsync();

            if (attendance.CompletedAt.HasValue || attendance.TotalSeconds >= attendance.RequiredSeconds)
            {
                await _certificateService.EnsureAuthorCertificateAsync(conference.Id, user.Id);
                await _certificateService.EnsureAttendeeCertificateAsync(conference.Id, user.Id);
            }

            return Json(new
            {
                ok = true,
                message = T("AttendanceStarted", "Katılım takibi başlatıldı."),
                totalSeconds = attendance.TotalSeconds,
                requiredSeconds = attendance.RequiredSeconds,
                completed = attendance.CompletedAt.HasValue || attendance.TotalSeconds >= attendance.RequiredSeconds
            });
        }

        [HttpPost("/{slug}/Attendance/Ping")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ping(string slug, Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    ok = false,
                    message = T("Unauthorized", "Oturum bulunamadı.")
                });
            }

            var conference = await GetValidConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = T("InvalidConference", "Geçersiz kongre.")
                });
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference, canonicalSlug);

            var access = await CanUserAttendConferenceAsync(user.Id, conference.Id);

            if (!access.CanAttend)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = access.Message
                });
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

            if (attendance.CompletedAt.HasValue || attendance.TotalSeconds >= attendance.RequiredSeconds)
            {
                await _certificateService.EnsureAuthorCertificateAsync(conference.Id, user.Id);
                await _certificateService.EnsureAttendeeCertificateAsync(conference.Id, user.Id);
            }

            return Json(new
            {
                ok = true,
                message = attendance.CompletedAt.HasValue
                    ? T("AttendanceCompleted", "Katılım tamamlandı.")
                    : T("AttendanceUpdated", "Katılım süresi güncellendi."),
                totalSeconds = attendance.TotalSeconds,
                requiredSeconds = attendance.RequiredSeconds,
                completed = attendance.CompletedAt.HasValue || attendance.TotalSeconds >= attendance.RequiredSeconds
            });
        }
    }
}