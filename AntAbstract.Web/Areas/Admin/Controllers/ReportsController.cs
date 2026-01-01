using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public ReportsController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/Reports")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var conf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return LocalRedirect(returnUrl);

                    return RedirectToAction(nameof(Index), new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
                }
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Raporlama Merkezi",
                Lead = "Verilerini incelemek istediğiniz kongreyi seçerek devam edin.",
                PostUrl = "/Admin/Reports/Select",
                SubmitText = "Raporları Görüntüle",
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Reports/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
        {
            var conf = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index), new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
        }

        [HttpGet("/{slug}/Admin/Reports")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId = null)
        {
            if (_tenantContext.Current == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                _selectedConferenceService.SetSelectedConferenceId(conferenceId.Value);

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            var confId = conference.Id;

            var totalSubmissions = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == confId)
                .CountAsync();

            var decidedSubmissions = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == confId && s.DecisionDate != null)
                .CountAsync();

            var totalAssignments = await (
                from ra in _context.ReviewAssignments.AsNoTracking()
                join s in _context.Submissions.AsNoTracking() on ra.SubmissionId equals s.Id
                where s.ConferenceId == confId
                select ra
            ).CountAsync();

            var assignedSubmissions = await (
                from ra in _context.ReviewAssignments.AsNoTracking()
                join s in _context.Submissions.AsNoTracking() on ra.SubmissionId equals s.Id
                where s.ConferenceId == confId
                select ra.SubmissionId
            ).Distinct().CountAsync();

            var registrations = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == confId)
                .Select(r => new { r.Amount, r.IsPaid })
                .ToListAsync();

            var statusCounts = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == confId)
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int CountOf(SubmissionStatus st) => statusCounts.FirstOrDefault(x => x.Status == st)?.Count ?? 0;

            var vm = new ReportsIndexViewModel
            {
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                ConferenceName = conference.Title,
                Slug = slug,

                TotalSubmissions = totalSubmissions,
                AssignedSubmissions = assignedSubmissions,
                DecidedSubmissions = decidedSubmissions,
                TotalAssignments = totalAssignments,

                TotalRegistrations = registrations.Count,
                TotalRevenue = registrations.Where(x => x.IsPaid).Sum(x => x.Amount),

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
            if (_tenantContext.Current == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                _selectedConferenceService.SetSelectedConferenceId(conferenceId.Value);

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

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

            var registrations = await _context.Registrations
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

            var ws1 = wb.Worksheets.Add("Submissions");
            ws1.Cell(1, 1).Value = "Id";
            ws1.Cell(1, 2).Value = "Title";
            ws1.Cell(1, 3).Value = "AuthorEmail";
            ws1.Cell(1, 4).Value = "Status";
            ws1.Cell(1, 5).Value = "CreatedAt";
            ws1.Cell(1, 6).Value = "DecisionDate";

            for (int i = 0; i < submissions.Count; i++)
            {
                var r = i + 2;
                ws1.Cell(r, 1).Value = submissions[i].Id.ToString();
                ws1.Cell(r, 2).Value = submissions[i].Title;
                ws1.Cell(r, 3).Value = submissions[i].AuthorEmail;
                ws1.Cell(r, 4).Value = submissions[i].Status;
                ws1.Cell(r, 5).Value = submissions[i].CreatedAt;
                ws1.Cell(r, 6).Value = submissions[i].DecisionDate;
            }
            ws1.Columns().AdjustToContents();

            var ws2 = wb.Worksheets.Add("Registrations");
            ws2.Cell(1, 1).Value = "Id";
            ws2.Cell(1, 2).Value = "Amount";
            ws2.Cell(1, 3).Value = "IsPaid";
            ws2.Cell(1, 4).Value = "RegistrationDate";
            ws2.Cell(1, 5).Value = "PaymentDate";
            ws2.Cell(1, 6).Value = "PaymentTransactionId";

            for (int i = 0; i < registrations.Count; i++)
            {
                var r = i + 2;
                ws2.Cell(r, 1).Value = registrations[i].Id.ToString();
                ws2.Cell(r, 2).Value = registrations[i].Amount;
                ws2.Cell(r, 3).Value = registrations[i].IsPaid ? "Paid" : "Unpaid";
                ws2.Cell(r, 4).Value = registrations[i].RegistrationDate;
                ws2.Cell(r, 5).Value = registrations[i].PaymentDate;
                ws2.Cell(r, 6).Value = registrations[i].PaymentTransactionId;
            }
            ws2.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var safeTitle = string.Join("_",
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
        public IActionResult LegacyRoot() => Redirect("/Admin/Reports");

        [HttpGet("/{slug}/Reports/Index")]
        public IActionResult LegacyTenant(string slug) => Redirect($"/{slug}/Admin/Reports");
    }
}
