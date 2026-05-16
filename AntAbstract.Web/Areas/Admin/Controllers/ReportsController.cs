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
    [Authorize(Roles = "Admin")]
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

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            var user = await GetCurrentUserAsync();

            if (user == null || !user.TenantId.HasValue)
            {
                return null;
            }

            return user.TenantId.Value;
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string slug)
        {
            if (_tenantContext.Current == null)
            {
                return false;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return false;
            }

            return tenantId.Value == _tenantContext.Current.Id;
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!tenantId.HasValue)
            {
                return query.Where(c => false);
            }

            return query.Where(c => c.TenantId == tenantId.Value);
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? _tenantContext.Current?.Slug ?? "";
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        private async Task<Conference?> GetAccessibleConferenceAsync(
            string slug,
            Guid? conferenceId)
        {
            if (!await CanAccessCurrentTenantAsync(slug))
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

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current!.Id);
        }

        [HttpGet("/Admin/Reports")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConference?.Tenant?.Slug != null)
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect($"/{selectedConference.Tenant.Slug}/Admin/Reports?conferenceId={selectedConference.Id}");
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
        public async Task<IActionResult> SelectConferencePost(
            Guid conferenceId,
            string? returnUrl = null)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null ||
                conference.Tenant == null ||
                string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect($"/{conference.Tenant.Slug}/Admin/Reports?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/Reports")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId = null)
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

            SetSelectedConferenceSession(conference);

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
                from reviewAssignment in _context.ReviewAssignments.AsNoTracking()
                join submission in _context.Submissions.AsNoTracking()
                    on reviewAssignment.SubmissionId equals submission.Id
                where submission.ConferenceId == confId
                select reviewAssignment
            ).CountAsync();

            var assignedSubmissions = await (
                from reviewAssignment in _context.ReviewAssignments.AsNoTracking()
                join submission in _context.Submissions.AsNoTracking()
                    on reviewAssignment.SubmissionId equals submission.Id
                where submission.ConferenceId == confId
                select reviewAssignment.SubmissionId
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
        public async Task<IActionResult> ExportExcel(
            string slug,
            Guid? conferenceId = null)
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

            SetSelectedConferenceSession(conference);

            var confId = conference.Id;

            var submissions = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Where(s => s.ConferenceId == confId)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    AuthorEmail = s.Author != null ? s.Author.Email : "",
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

            using var workbook = new XLWorkbook();

            var submissionsSheet = workbook.Worksheets.Add(T("Excel_SubmissionsSheet", "Submissions"));

            submissionsSheet.Cell(1, 1).Value = T("Excel_Id", "Id");
            submissionsSheet.Cell(1, 2).Value = T("Excel_Title", "Title");
            submissionsSheet.Cell(1, 3).Value = T("Excel_AuthorEmail", "Author Email");
            submissionsSheet.Cell(1, 4).Value = T("Excel_Status", "Status");
            submissionsSheet.Cell(1, 5).Value = T("Excel_CreatedAt", "Created At");
            submissionsSheet.Cell(1, 6).Value = T("Excel_DecisionDate", "Decision Date");

            for (var i = 0; i < submissions.Count; i++)
            {
                var row = i + 2;

                submissionsSheet.Cell(row, 1).Value = submissions[i].Id.ToString();
                submissionsSheet.Cell(row, 2).Value = submissions[i].Title ?? "";
                submissionsSheet.Cell(row, 3).Value = submissions[i].AuthorEmail ?? "";
                submissionsSheet.Cell(row, 4).Value = submissions[i].Status;
                submissionsSheet.Cell(row, 5).Value = submissions[i].CreatedAt;

                if (submissions[i].DecisionDate.HasValue)
                {
                    submissionsSheet.Cell(row, 6).Value = submissions[i].DecisionDate.Value;
                }
                else
                {
                    submissionsSheet.Cell(row, 6).Value = "";
                }
            }

            submissionsSheet.Columns().AdjustToContents();

            var registrationsSheet = workbook.Worksheets.Add(T("Excel_RegistrationsSheet", "Registrations"));

            registrationsSheet.Cell(1, 1).Value = T("Excel_Id", "Id");
            registrationsSheet.Cell(1, 2).Value = T("Excel_Amount", "Amount");
            registrationsSheet.Cell(1, 3).Value = T("Excel_IsPaid", "Is Paid");
            registrationsSheet.Cell(1, 4).Value = T("Excel_RegistrationDate", "Registration Date");
            registrationsSheet.Cell(1, 5).Value = T("Excel_PaymentDate", "Payment Date");
            registrationsSheet.Cell(1, 6).Value = T("Excel_PaymentTransactionId", "Payment Transaction Id");

            for (var i = 0; i < registrationsData.Count; i++)
            {
                var row = i + 2;

                registrationsSheet.Cell(row, 1).Value = registrationsData[i].Id.ToString();
                registrationsSheet.Cell(row, 2).Value = registrationsData[i].Amount;
                registrationsSheet.Cell(row, 3).Value = registrationsData[i].IsPaid
                    ? T("Excel_Paid", "Paid")
                    : T("Excel_Unpaid", "Unpaid");

                registrationsSheet.Cell(row, 4).Value = registrationsData[i].RegistrationDate;

                if (registrationsData[i].PaymentDate.HasValue)
                {
                    registrationsSheet.Cell(row, 5).Value = registrationsData[i].PaymentDate.Value;
                }
                else
                {
                    registrationsSheet.Cell(row, 5).Value = "";
                }

                registrationsSheet.Cell(row, 6).Value = registrationsData[i].PaymentTransactionId ?? "";
            }

            registrationsSheet.Columns().AdjustToContents();

            using var memoryStream = new MemoryStream();

            workbook.SaveAs(memoryStream);

            var safeTitle = string.Join(
                "_",
                (conference.Title ?? "conference")
                    .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

            var fileName = $"reports_{safeTitle}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";

            return File(
                memoryStream.ToArray(),
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