using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class AttendanceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IAuditService _audit;

        public AttendanceController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IAdminTenantAccessService tenantAccess,
            IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _tenantAccess = tenantAccess;
            _audit = audit;
        }

        // ── QR Tarayıcı Sayfası ─────────────────────────────────────────────────

        [HttpGet("/Admin/Attendance/Scan")]
        [HttpGet("/{slug}/Admin/Attendance/Scan")]
        public async Task<IActionResult> Scan(string? slug, Guid? conferenceId)
        {
            var conferences = await _tenantAccess
                .GetAccessibleConferenceQueryAsync(User);

            var conferenceList = await conferences
                .OrderByDescending(c => c.StartDate)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            ViewBag.Conferences = conferenceList;
            ViewBag.SelectedConferenceId = conferenceId;
            ViewBag.Slug = slug;
            return View();
        }

        // ── Check-In Doğrulama (AJAX/POST) ──────────────────────────────────────

        [HttpPost("/Admin/Attendance/CheckIn")]
        [HttpPost("/{slug}/Admin/Attendance/CheckIn")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(string? slug, string qrToken, Guid conferenceId)
        {
            if (string.IsNullOrWhiteSpace(qrToken))
                return Json(new { success = false, message = "QR token boş." });

            // Token ile kayıt bul
            var registration = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r =>
                    r.QrToken == qrToken &&
                    r.ConferenceId == conferenceId);

            if (registration == null)
                return Json(new { success = false, message = "Geçersiz QR kod veya bu kongreye ait kayıt bulunamadı." });

            if (!registration.IsPaid)
                return Json(new { success = false, message = $"Kayıt ödenmemiş: {registration.AppUser?.Email}" });

            // Daha önce check-in yapıldı mı?
            var alreadyCheckedIn = registration.CheckedInAt.HasValue;

            if (!alreadyCheckedIn)
            {
                // Tracked kayıt al
                var trackedReg = await _context.Registrations
                    .FirstOrDefaultAsync(r => r.Id == registration.Id);

                if (trackedReg != null)
                {
                    var adminUser = await _userManager.GetUserAsync(User);
                    trackedReg.CheckedInAt = DateTime.UtcNow;
                    trackedReg.CheckedInByUserId = adminUser?.Id;
                    await _context.SaveChangesAsync();

                    await _audit.LogAsync(
                        category: "Attendance",
                        action: "CheckIn",
                        userId: adminUser?.Id,
                        userName: adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                        entityType: "Registration",
                        entityId: registration.Id.ToString(),
                        description: $"Check-in: {registration.AppUser?.Email} — {registration.AppUser?.FirstName} {registration.AppUser?.LastName}",
                        conferenceId: conferenceId,
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
                }
            }

            var fullName = $"{registration.AppUser?.FirstName} {registration.AppUser?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName)) fullName = registration.AppUser?.Email ?? "?";

            return Json(new
            {
                success = true,
                alreadyCheckedIn,
                name = fullName,
                email = registration.AppUser?.Email,
                registrationType = registration.RegistrationType?.Name,
                checkedInAt = alreadyCheckedIn
                    ? registration.CheckedInAt?.ToString("dd.MM.yyyy HH:mm")
                    : DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm"),
                message = alreadyCheckedIn
                    ? $"⚠️ Bu katılımcı daha önce check-in yapmış ({registration.CheckedInAt:dd.MM.yyyy HH:mm})"
                    : $"✅ Giriş onaylandı: {fullName}"
            });
        }

        // ── Katılımcı Listesi (Admin) ────────────────────────────────────────────

        [HttpGet("/Admin/Attendance")]
        [HttpGet("/{slug}/Admin/Attendance")]
        public async Task<IActionResult> Index(string? slug, Guid? conferenceId)
        {
            var conferences = await _tenantAccess
                .GetAccessibleConferenceQueryAsync(User);

            Conference? conference = null;
            if (conferenceId.HasValue && conferenceId != Guid.Empty)
            {
                conference = await conferences.FirstOrDefaultAsync(c => c.Id == conferenceId);
            }

            if (conference == null)
            {
                conference = await conferences
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefaultAsync();
            }

            if (conference == null)
            {
                ViewBag.NoConference = true;
                return View(Array.Empty<Registration>());
            }

            var registrations = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Where(r => r.ConferenceId == conference.Id && r.IsPaid)
                .OrderBy(r => r.AppUser!.LastName)
                .ThenBy(r => r.AppUser!.FirstName)
                .ToListAsync();

            // Slug için Tenant'ı ayrıca yükle
            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == conference.TenantId);

            ViewBag.Conference = conference;
            ViewBag.Slug = slug ?? tenant?.Slug;
            ViewBag.TotalCheckedIn = registrations.Count(r => r.CheckedInAt.HasValue);
            ViewBag.TotalRegistrations = registrations.Count;

            return View(registrations);
        }

        // ── Badge baskı sayfası ───────────────────────────────────────────────────
        [HttpGet("/Admin/Attendance/Badges")]
        [HttpGet("/{slug}/Admin/Attendance/Badges")]
        public async Task<IActionResult> Badges(string? slug, Guid? conferenceId, string? filter)
        {
            var conferences = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            Conference? conference = null;
            if (conferenceId.HasValue && conferenceId != Guid.Empty)
                conference = await conferences.FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
                conference = await conferences.OrderByDescending(c => c.StartDate).FirstOrDefaultAsync();

            if (conference == null)
            {
                ViewBag.NoConference = true;
                return View(Array.Empty<Registration>());
            }

            var query = _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Where(r => r.ConferenceId == conference.Id && r.IsPaid);

            if (filter == "checkedin")
                query = query.Where(r => r.CheckedInAt.HasValue);

            var registrations = await query
                .OrderBy(r => r.AppUser!.LastName)
                .ThenBy(r => r.AppUser!.FirstName)
                .ToListAsync();

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == conference.TenantId);

            ViewBag.Conference = conference;
            ViewBag.Slug = slug ?? tenant?.Slug;
            ViewBag.Filter = filter ?? "all";

            return View(registrations);
        }
    }
}
