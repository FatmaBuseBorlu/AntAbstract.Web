using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public ReportsController(AppDbContext context, TenantContext tenantContext, ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/Reports")]
        public async Task<IActionResult> SelectConference()
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
                    return Redirect($"/{conf.Tenant.Slug}/Admin/Reports?conferenceId={conf.Id}");
                }
            }

            // mevcut kodun devamı
            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Raporlama Merkezi",
                Lead = "Verilerini incelemek istediğiniz kongreyi seçerek devam edin.",
                PostUrl = "/admin/reports/select",
                SubmitText = "Raporları Görüntüle",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }


        [HttpPost("/Admin/Reports/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId)
        {
            var conf = await _context.Conferences.Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null) return NotFound();

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);

            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Admin/Reports?conferenceId={conf.Id}");
        }

        [HttpGet("/{slug}/Admin/Reports/Excel")]
        public async Task<IActionResult> ExportExcel(string slug)
        {
            if (_tenantContext.Current == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports" });

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
                .Where(s => s.ConferenceId == confId)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    AuthorEmail = s.Author != null ? s.Author.Email : null,
                    Status = s.Status.ToString(),
                    s.CreatedAt,
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

            using var wb = new ClosedXML.Excel.XLWorkbook();

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