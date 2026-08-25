using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels.Admin.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    public class SystemReportsController : Controller
    {
        private readonly AppDbContext _context;

        public SystemReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("/Admin/SystemReports")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;

            // ── Scalar count'lar — DB aggregate, RAM'e çekme ────────────────
            var totalInstitutions = await _context.Tenants.CountAsync();
            var totalConferences = await _context.Conferences.CountAsync();
            var activeConferences = await _context.Conferences
                .CountAsync(c => c.StartDate.Date <= today && c.EndDate.Date >= today);
            var totalUsers = await _context.Users.CountAsync();
            var totalSubmissions = await _context.Submissions.CountAsync();
            var assignedSubmissions = await _context.Submissions
                .CountAsync(s => s.ReviewAssignments.Any());

            var decisionCompletedStatuses = new[]
            {
                SubmissionStatus.Accepted, SubmissionStatus.Rejected,
                SubmissionStatus.RevisionRequired
            };
            var decisionCompletedSubmissions = await _context.Submissions
                .CountAsync(s => decisionCompletedStatuses.Contains(s.Status));
            var decisionPendingSubmissions = await _context.Submissions
                .CountAsync(s => s.ReviewAssignments.Any() &&
                                 !decisionCompletedStatuses.Contains(s.Status));

            var totalRegistrations = await _context.Registrations.CountAsync();
            var paidRegistrations = await _context.Registrations.CountAsync(r => r.IsPaid);

            // SQLite decimal uzerinde SUM yapamiyor; tutarlar cekilip bellekte toplaniyor.
            var completedRevenue = (await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Select(p => p.Amount)
                .ToListAsync())
                .Sum();

            var paidRegRevenue = completedRevenue > 0 ? completedRevenue
                : (await _context.Registrations
                    .Where(r => r.IsPaid)
                    .Select(r => r.Amount)
                    .ToListAsync())
                    .Sum();

            // ── Rol sayıları (UserRoles × Roles join — N+1 yok) ──────────────
            var roleCounts = await _context.UserRoles
                .Join(_context.Roles, ur => ur.RoleId, r => r.Id,
                    (ur, r) => new { r.Name, ur.UserId })
                .Where(x => x.Name != null)
                .GroupBy(x => x.Name!)
                .Select(g => new { Role = g.Key, Count = g.Select(x => x.UserId).Distinct().Count() })
                .ToDictionaryAsync(g => g.Role, g => g.Count);

            // ── Tenant raporu — top 8 (GROUP BY DB'de) ───────────────────────
            var confByTenant = await _context.Conferences
                .GroupBy(c => c.TenantId)
                .Select(g => new { TenantId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.TenantId, g => g.Count);

            var usersByTenant = await _context.Users
                .Where(u => u.TenantId.HasValue)
                .GroupBy(u => u.TenantId!.Value)
                .Select(g => new { TenantId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.TenantId, g => g.Count);

            var tenantReports = await _context.Tenants
                .AsNoTracking()
                .Select(t => new TenantConferenceReportItem
                {
                    TenantId = t.Id,
                    TenantName = t.Name ?? "",
                    Slug = t.Slug ?? "",
                    ConferenceCount = _context.Conferences.Count(c => c.TenantId == t.Id),
                    UserCount = _context.Users.Count(u => u.TenantId == t.Id)
                })
                .OrderByDescending(x => x.ConferenceCount)
                .ThenByDescending(x => x.UserCount)
                .Take(8)
                .ToListAsync();

            // ── Kongre-başvuru raporu — top 8 ────────────────────────────────
            var subCountByConf = await _context.Submissions
                .GroupBy(s => s.ConferenceId)
                .Select(g => new { ConferenceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ConferenceId, g => g.Count);

            var conferenceSubmissionReports = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => _context.Submissions.Count(s => s.ConferenceId == c.Id))
                .ThenBy(c => c.Title)
                .Take(8)
                .Select(c => new ConferenceSubmissionReportItem
                {
                    ConferenceId = c.Id,
                    ConferenceTitle = c.Title ?? "",
                    TenantName = c.Tenant != null ? c.Tenant.Name : "",
                    Slug = c.Tenant != null ? c.Tenant.Slug : string.Empty,
                    SubmissionCount = _context.Submissions.Count(s => s.ConferenceId == c.Id)
                })
                .ToListAsync();

            // ── Son 6 kongre ──────────────────────────────────────────────────
            var recentConferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .Take(6)
                .Select(c => new RecentConferenceReportItem
                {
                    Id = c.Id,
                    Title = c.Title ?? "",
                    TenantName = c.Tenant != null ? c.Tenant.Name : "",
                    Slug = c.Tenant != null ? c.Tenant.Slug : string.Empty,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate
                })
                .ToListAsync();

            // ── Son 6 kullanıcı ───────────────────────────────────────────────
            var recentUsers = await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.Id) // Id sıralaması yeterli proxy
                .Take(6)
                .Select(u => new RecentUserReportItem
                {
                    Id = u.Id,
                    FullName = (u.FirstName + " " + u.LastName).Trim() != ""
                        ? (u.FirstName + " " + u.LastName).Trim()
                        : u.UserName ?? u.Email ?? "Kullanıcı",
                    Email = u.Email ?? "",
                    TenantName = u.TenantId.HasValue
                        ? (_context.Tenants
                            .Where(t => t.Id == u.TenantId.Value)
                            .Select(t => t.Name)
                            .FirstOrDefault() ?? "Kurum yok")
                        : "Kurum yok",
                    CreatedDate = null
                })
                .ToListAsync();

            var model = new SystemReportsIndexViewModel
            {
                TotalInstitutions = totalInstitutions,
                TotalConferences = totalConferences,
                ActiveConferences = activeConferences,

                TotalUsers = totalUsers,
                TotalAdmins = roleCounts.GetValueOrDefault("Admin"),
                TotalAuthors = roleCounts.GetValueOrDefault("Author"),
                TotalReviewers = roleCounts.GetValueOrDefault("Referee") +
                                 roleCounts.GetValueOrDefault("Hakem") +
                                 roleCounts.GetValueOrDefault("Reviewer"),

                TotalSubmissions = totalSubmissions,
                AssignedSubmissions = assignedSubmissions,
                DecisionPendingSubmissions = decisionPendingSubmissions,
                DecisionCompletedSubmissions = decisionCompletedSubmissions,

                TotalRegistrations = totalRegistrations,
                PaidRegistrations = paidRegistrations,
                PendingPayments = totalRegistrations - paidRegistrations,

                TotalRevenue = paidRegRevenue,

                RecentUsers = recentUsers,
                RecentConferences = recentConferences,
                ConferenceSubmissionReports = conferenceSubmissionReports,
                TenantConferenceReports = tenantReports
            };

            return View("~/Areas/Admin/Views/SystemReports/Index.cshtml", model);
        }

        [HttpGet("/Admin/Reports/System")]
        public IActionResult LegacySystemReports()
        {
            return RedirectToAction(nameof(Index));
        }

    }
}
