using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels.Admin.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public SystemReportsController(
            AppDbContext context,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("/Admin/SystemReports")]
        public async Task<IActionResult> Index()
        {
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

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var authors = await _userManager.GetUsersInRoleAsync("Author");
            var reviewers = await _userManager.GetUsersInRoleAsync("Referee");

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

            var recentUsers = users
                .Take(6)
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
                        CreatedDate = null
                    };
                })
                .ToList();

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
                TotalUsers = users.Count,
                TotalAdmins = admins.Count,
                TotalAuthors = authors.Count,
                TotalReviewers = reviewers.Count,
                TotalSubmissions = submissions.Count,
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
    }
}