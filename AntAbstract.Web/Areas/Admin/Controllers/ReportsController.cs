using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Reports;
using AntAbstract.Web.Models.ViewModels.Shared;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<ReportsController> _localizer;

        public ReportsController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<ReportsController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            if (_tenantContext.Current == null)
            {
                return false;
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return false;
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                return true;
            }

            return user.TenantId.HasValue &&
                   user.TenantId.Value == _tenantContext.Current.Id;
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!isAdmin && user?.TenantId != null)
            {
                query = query.Where(c => c.TenantId == user.TenantId.Value);
            }
            else if (!isAdmin && user?.TenantId == null)
            {
                query = query.Where(c => false);
            }

            return query;
        }

        private async Task<Conference?> GetAccessibleConferenceAsync(string slug, Guid? conferenceId)
        {
            if (_tenantContext.Current == null)
            {
                return null;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                return null;
            }

            Guid? selectedConferenceId = null;

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                selectedConferenceId = conferenceId.Value;
            }
            else
            {
                selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            }

            if (selectedConferenceId == null || selectedConferenceId.Value == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);
        }

        [HttpGet("/Admin/Reports")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId != null)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var conf = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
                    HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            slug = conf.Tenant.Slug,
                            conferenceId = conf.Id
                        });
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = T("SelectConference_Lead", "Raporları görüntülemek için önce kongre seçiniz."),
                PostUrl = "/Admin/Reports/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Reports/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
        {
            var query = await GetAccessibleConferenceQueryAsync();

            var conf = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    slug = conf.Tenant.Slug,
                    conferenceId = conf.Id
                });
        }

        [HttpGet("/{slug}/Admin/Reports")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            var confId = conference.Id;

            var totalSubmissions = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == confId)
                .CountAsync();

            var decidedSubmissions = await _context.Submissions
                .AsNoTracking()
                .Where(s =>
                    s.ConferenceId == confId &&
                    s.DecisionDate != null)
                .CountAsync();

            var totalAssignments = await (
                from ra in _context.ReviewAssignments.AsNoTracking()
                join s in _context.Submissions.AsNoTracking()
                    on ra.SubmissionId equals s.Id
                where s.ConferenceId == confId
                select ra
            ).CountAsync();

            var assignedSubmissions = await (
                from ra in _context.ReviewAssignments.AsNoTracking()
                join s in _context.Submissions.AsNoTracking()
                    on ra.SubmissionId equals s.Id
                where s.ConferenceId == confId
                select ra.SubmissionId
            ).Distinct().CountAsync();

            var registrations = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == confId)
                .Select(r => new
                {
                    r.Amount,
                    r.IsPaid
                })
                .ToListAsync();

            var statusCounts = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == confId)
                .GroupBy(s => s.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            int CountOf(SubmissionStatus status)
            {
                return statusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
            }

            var vm = new ReportsIndexViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                ConferenceName = conference.Title ?? "",
                Slug = slug,

                TotalSubmissions = totalSubmissions,
                AssignedSubmissions = assignedSubmissions,
                DecidedSubmissions = decidedSubmissions,
                TotalAssignments = totalAssignments,

                TotalRegistrations = registrations.Count,
                TotalRevenue = registrations
                    .Where(x => x.IsPaid)
                    .Sum(x => x.Amount),

                NewCount = CountOf(SubmissionStatus.New),
                PendingCount = CountOf(SubmissionStatus.Pending),
                UnderReviewCount = CountOf(SubmissionStatus.UnderReview),
                AcceptedCount = CountOf(SubmissionStatus.Accepted),
                RejectedCount = CountOf(SubmissionStatus.Rejected),
                RevisionRequiredCount = CountOf(SubmissionStatus.RevisionRequired)
            };

            return View("~/Areas/Admin/Views/Reports/Index.cshtml", vm);
        }

        [HttpGet("/{slug}/Admin/Reports/Excel")]
        public async Task<IActionResult> ExportExcel(string slug, Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Reports" });
            }

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            var confId = conference.Id;

            var submissions = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == confId)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    AuthorEmail = s.Author != null ? s.Author.Email : null,
                    Status = s.Status.ToString(),
                    CreatedAt = s.CreatedDate,
                    s.DecisionDate
                })
                .ToListAsync();

            var registrationsData = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == confId)
                .Select(r => new
                {
                    r.Id,
                    r.Amount,
                    r.IsPaid,
                    r.RegistrationDate,
                    r.PaymentDate,
                    r.PaymentTransactionId
                })
                .ToListAsync();

            using var wb = new XLWorkbook();

            var ws1 = wb.Worksheets.Add(T("Excel_SubmissionsSheet", "Submissions"));
            ws1.Cell(1, 1).Value = T("Excel_Id", "Id");
            ws1.Cell(1, 2).Value = T("Excel_Title", "Title");
            ws1.Cell(1, 3).Value = T("Excel_AuthorEmail", "Author Email");
            ws1.Cell(1, 4).Value = T("Excel_Status", "Status");
            ws1.Cell(1, 5).Value = T("Excel_CreatedAt", "Created At");
            ws1.Cell(1, 6).Value = T("Excel_DecisionDate", "Decision Date");

            for (int i = 0; i < submissions.Count; i++)
            {
                var row = i + 2;

                ws1.Cell(row, 1).Value = submissions[i].Id.ToString();
                ws1.Cell(row, 2).Value = submissions[i].Title;
                ws1.Cell(row, 3).Value = submissions[i].AuthorEmail;
                ws1.Cell(row, 4).Value = submissions[i].Status;
                ws1.Cell(row, 5).Value = submissions[i].CreatedAt;
                ws1.Cell(row, 6).Value = submissions[i].DecisionDate;
            }

            ws1.Columns().AdjustToContents();

            var ws2 = wb.Worksheets.Add(T("Excel_RegistrationsSheet", "Registrations"));
            ws2.Cell(1, 1).Value = T("Excel_Id", "Id");
            ws2.Cell(1, 2).Value = T("Excel_Amount", "Amount");
            ws2.Cell(1, 3).Value = T("Excel_IsPaid", "Is Paid");
            ws2.Cell(1, 4).Value = T("Excel_RegistrationDate", "Registration Date");
            ws2.Cell(1, 5).Value = T("Excel_PaymentDate", "Payment Date");
            ws2.Cell(1, 6).Value = T("Excel_PaymentTransactionId", "Payment Transaction Id");

            for (int i = 0; i < registrationsData.Count; i++)
            {
                var row = i + 2;

                ws2.Cell(row, 1).Value = registrationsData[i].Id.ToString();
                ws2.Cell(row, 2).Value = registrationsData[i].Amount;
                ws2.Cell(row, 3).Value = registrationsData[i].IsPaid
                    ? T("Excel_Paid", "Paid")
                    : T("Excel_Unpaid", "Unpaid");
                ws2.Cell(row, 4).Value = registrationsData[i].RegistrationDate;
                ws2.Cell(row, 5).Value = registrationsData[i].PaymentDate;
                ws2.Cell(row, 6).Value = registrationsData[i].PaymentTransactionId;
            }

            ws2.Columns().AdjustToContents();

            using var ms = new MemoryStream();

            wb.SaveAs(ms);

            var safeTitle = string.Join(
                "_",
                (conference.Title ?? "conference")
                    .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

            var fileName = $"reports_{safeTitle}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";

            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }

        [HttpGet("/Reports/Index")]
        public IActionResult LegacyRoot()
        {
            return Redirect("/Admin/Reports");
        }

        [HttpGet("/{slug}/Reports/Index")]
        public IActionResult LegacyTenant(string slug)
        {
            return Redirect($"/{slug}/Admin/Reports");
        }
    }
}