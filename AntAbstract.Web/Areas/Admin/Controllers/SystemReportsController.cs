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
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private static readonly string[] ReviewerRoleNames =
        {
            "Referee",
            "Hakem",
            "Reviewer"
        };

        public SystemReportsController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("/Admin/SystemReports")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .OrderByDescending(x => x.StartDate)
                .ToListAsync();

            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(x => x.Email)
                .ToListAsync();

            var submissions = await _context.Submissions
                .AsNoTracking()
                .Include(x => x.Conference)
                    .ThenInclude(x => x.Tenant)
                .Include(x => x.ReviewAssignments)
                .ToListAsync();

            var registrations = await _context.Registrations
                .AsNoTracking()
                .Include(x => x.RegistrationType)
                .Include(x => x.Conference)
                    .ThenInclude(x => x.Tenant)
                .ToListAsync();

            var completedPayments = await _context.Payments
                .AsNoTracking()
                .Where(x => x.Status == PaymentStatus.Completed)
                .ToListAsync();

            var admins = await GetUsersInRoleSafeAsync("Admin");
            var authors = await GetUsersInRoleSafeAsync("Author");
            var reviewers = await GetReviewerUsersAsync();

            var activeConferences = conferences.Count(conference =>
                conference.StartDate.Date <= today &&
                conference.EndDate.Date >= today);

            var assignedSubmissions = submissions.Count(submission =>
                submission.ReviewAssignments != null &&
                submission.ReviewAssignments.Any());

            var decisionCompletedSubmissions = submissions.Count(submission =>
                IsDecisionCompletedStatus(submission.Status));

            var decisionPendingSubmissions = submissions.Count(submission =>
                submission.ReviewAssignments != null &&
                submission.ReviewAssignments.Any() &&
                !IsDecisionCompletedStatus(submission.Status));

            var tenantReports = tenants
                .Select(tenant => new TenantConferenceReportItem
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.Name ?? "",
                    Slug = tenant.Slug ?? "",
                    ConferenceCount = conferences.Count(c => c.TenantId == tenant.Id),
                    UserCount = users.Count(u => u.TenantId.HasValue && u.TenantId.Value == tenant.Id)
                })
                .OrderByDescending(x => x.ConferenceCount)
                .ThenByDescending(x => x.UserCount)
                .Take(8)
                .ToList();

            var conferenceSubmissionReports = conferences
                .Select(conference => new ConferenceSubmissionReportItem
                {
                    ConferenceId = conference.Id,
                    ConferenceTitle = conference.Title ?? "",
                    TenantName = conference.Tenant?.Name ?? "",
                    Slug = conference.Tenant?.Slug ?? "",
                    SubmissionCount = submissions.Count(s => s.ConferenceId == conference.Id)
                })
                .OrderByDescending(x => x.SubmissionCount)
                .ThenBy(x => x.ConferenceTitle)
                .Take(8)
                .ToList();

            var recentConferences = conferences
                .Take(6)
                .Select(conference => new RecentConferenceReportItem
                {
                    Id = conference.Id,
                    Title = conference.Title ?? "",
                    TenantName = conference.Tenant?.Name ?? "",
                    Slug = conference.Tenant?.Slug ?? "",
                    StartDate = conference.StartDate,
                    EndDate = conference.EndDate
                })
                .ToList();

            var recentUsers = BuildRecentUsers(users, tenants);

            var paidRegistrationRevenue = registrations
                .Where(x => x.IsPaid)
                .Sum(x => x.Amount);

            var completedPaymentRevenue = completedPayments
                .Sum(x => x.Amount);

            var totalRevenue = completedPaymentRevenue > 0
                ? completedPaymentRevenue
                : paidRegistrationRevenue;

            var model = new SystemReportsIndexViewModel
            {
                TotalInstitutions = tenants.Count,
                TotalConferences = conferences.Count,
                ActiveConferences = activeConferences,

                TotalUsers = users.Count,
                TotalAdmins = admins.Count,
                TotalAuthors = authors.Count,
                TotalReviewers = reviewers.Count,

                TotalSubmissions = submissions.Count,
                AssignedSubmissions = assignedSubmissions,
                DecisionPendingSubmissions = decisionPendingSubmissions,
                DecisionCompletedSubmissions = decisionCompletedSubmissions,

                TotalRegistrations = registrations.Count,
                PaidRegistrations = registrations.Count(x => x.IsPaid),
                PendingPayments = registrations.Count(x => !x.IsPaid),

                TotalRevenue = totalRevenue,

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

        private async Task<List<AppUser>> GetUsersInRoleSafeAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return new List<AppUser>();
            }

            var roleExists = await _roleManager.RoleExistsAsync(roleName);

            if (!roleExists)
            {
                return new List<AppUser>();
            }

            var users = await _userManager.GetUsersInRoleAsync(roleName);

            return users.ToList();
        }

        private async Task<List<AppUser>> GetReviewerUsersAsync()
        {
            var reviewerUsers = new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in ReviewerRoleNames)
            {
                var usersInRole = await GetUsersInRoleSafeAsync(roleName);

                foreach (var user in usersInRole)
                {
                    if (user == null || string.IsNullOrWhiteSpace(user.Id))
                    {
                        continue;
                    }

                    reviewerUsers[user.Id] = user;
                }
            }

            return reviewerUsers.Values.ToList();
        }

        private static bool IsDecisionCompletedStatus(SubmissionStatus status)
        {
            var statusName = status.ToString();

            return statusName.Contains("Accepted", StringComparison.OrdinalIgnoreCase) ||
                   statusName.Contains("Rejected", StringComparison.OrdinalIgnoreCase) ||
                   statusName.Contains("Revision", StringComparison.OrdinalIgnoreCase) ||
                   statusName.Contains("Published", StringComparison.OrdinalIgnoreCase) ||
                   statusName.Contains("Completed", StringComparison.OrdinalIgnoreCase);
        }

        private static List<RecentUserReportItem> BuildRecentUsers(
            List<AppUser> users,
            List<Tenant> tenants)
        {
            var createdDateProperty = typeof(AppUser).GetProperty("CreatedDate");

            var orderedUsers = users
                .OrderByDescending(user => GetUserCreatedDate(user, createdDateProperty) ?? DateTime.MinValue)
                .ThenBy(user => user.Email)
                .Take(6)
                .ToList();

            return orderedUsers
                .Select(user =>
                {
                    var tenant = user.TenantId.HasValue
                        ? tenants.FirstOrDefault(x => x.Id == user.TenantId.Value)
                        : null;

                    var fullName = $"{user.FirstName} {user.LastName}".Trim();

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        fullName = user.UserName ?? user.Email ?? "Kullanıcı";
                    }

                    return new RecentUserReportItem
                    {
                        Id = user.Id,
                        FullName = fullName,
                        Email = user.Email ?? "",
                        TenantName = tenant?.Name ?? "Kurum yok",
                        CreatedDate = GetUserCreatedDate(user, createdDateProperty)
                    };
                })
                .ToList();
        }

        private static DateTime? GetUserCreatedDate(
            AppUser user,
            System.Reflection.PropertyInfo? createdDateProperty)
        {
            if (createdDateProperty == null)
            {
                return null;
            }

            var value = createdDateProperty.GetValue(user);

            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            return null;
        }
    }
}