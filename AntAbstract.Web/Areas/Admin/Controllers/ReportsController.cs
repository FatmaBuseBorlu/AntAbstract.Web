using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Reports;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Security;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdminOnly)]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IStringLocalizer<ReportsController> _localizer;

        public ReportsController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IAdminTenantAccessService tenantAccess,
            IStringLocalizer<ReportsController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _tenantAccess = tenantAccess;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string slug)
        {
            return await _tenantAccess.CanAccessCurrentTenantAsync(
                User,
                slug,
                allowSuperAdmin: false);
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var query = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            return query
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();
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
                    r.IsPaid,
                    TypeName = r.RegistrationType.Name,
                    r.RegistrationType.Currency
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
                PaidRegistrations = registrations.Count(x => x.IsPaid),
                UnpaidRegistrations = registrations.Count(x => !x.IsPaid),
                UnpaidRevenue = registrations
                    .Where(x => !x.IsPaid)
                    .Sum(x => x.Amount),
                RevenueCurrency = registrations
                    .Select(x => x.Currency)
                    .FirstOrDefault() ?? "TRY",
                FinanceByCategory = registrations
                    .GroupBy(x => x.TypeName)
                    .Select(g => new FinanceCategoryItem
                    {
                        Label = g.Key,
                        PaidAmount = g.Where(x => x.IsPaid).Sum(x => x.Amount),
                        UnpaidAmount = g.Where(x => !x.IsPaid).Sum(x => x.Amount),
                        PaidCount = g.Count(x => x.IsPaid),
                        UnpaidCount = g.Count(x => !x.IsPaid)
                    })
                    .OrderByDescending(x => x.PaidAmount + x.UnpaidAmount)
                    .ToList(),

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
                    submissionsSheet.Cell(row, 6).Value = submissions[i].DecisionDate!.Value;
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
                    registrationsSheet.Cell(row, 5).Value = registrationsData[i].PaymentDate!.Value;
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

        [HttpGet("/{slug}/Admin/Reports/Pdf")]
        public async Task<IActionResult> ExportPdf(
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
                .OrderBy(s => s.CreatedDate)
                .Select(s => new
                {
                    s.Title,
                    AuthorName = s.Author != null
                        ? (s.Author.FirstName + " " + s.Author.LastName).Trim()
                        : (s.Author != null ? s.Author.Email : ""),
                    AuthorEmail = s.Author != null ? s.Author.Email : "",
                    s.Status,
                    s.CreatedDate,
                    s.DecisionDate
                })
                .ToListAsync();

            var totalCount = submissions.Count;
            var acceptedCount = submissions.Count(s => s.Status == SubmissionStatus.Accepted);
            var rejectedCount = submissions.Count(s => s.Status == SubmissionStatus.Rejected);
            var pendingCount = submissions.Count(s => s.Status != SubmissionStatus.Accepted && s.Status != SubmissionStatus.Rejected);

            const string Navy = "#1a2d5a";
            const string Gold = "#b8972a";
            const string Cream = "#fdf8ee";

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(18);
                    page.MarginBottom(18);
                    page.MarginHorizontal(22);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Background(Navy).PaddingVertical(10).PaddingHorizontal(14).Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                inner.Item().Text(conference.Title ?? "Kongre Bildiri Raporu")
                                    .FontColor(Colors.White).FontSize(14).Bold();
                                inner.Item().Text($"Üretim tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}")
                                    .FontColor("#cccccc").FontSize(8);
                            });
                            row.ConstantItem(90).AlignRight().AlignMiddle()
                                .Text("BİLDİRİ RAPORU").FontColor(Gold).Bold().FontSize(10);
                        });

                        col.Item().Background(Gold).Height(3);

                        // Özet istatistik satırı
                        col.Item().Background(Cream).PaddingVertical(7).PaddingHorizontal(14).Row(row =>
                        {
                            void StatBox(string label, string val, string color)
                            {
                                row.RelativeItem().Border(1).BorderColor("#dddddd").PaddingVertical(4).PaddingHorizontal(8).Column(c =>
                                {
                                    c.Item().Text(label).FontSize(7).FontColor("#666666");
                                    c.Item().Text(val).FontSize(16).Bold().FontColor(color);
                                });
                            }
                            StatBox("Toplam Bildiri", totalCount.ToString(), Navy);
                            row.ConstantItem(6);
                            StatBox("Kabul", acceptedCount.ToString(), "#16a34a");
                            row.ConstantItem(6);
                            StatBox("Red", rejectedCount.ToString(), "#dc2626");
                            row.ConstantItem(6);
                            StatBox("Beklemede", pendingCount.ToString(), "#ca8a04");
                        });

                        col.Item().Height(6);
                    });

                    page.Content().Column(col =>
                    {
                        // Tablo başlığı
                        col.Item().Background(Navy).PaddingVertical(5).PaddingHorizontal(6).Row(row =>
                        {
                            row.ConstantItem(22).Text("#").FontColor(Colors.White).Bold().FontSize(8);
                            row.RelativeItem(4).Text("Bildiri Başlığı").FontColor(Colors.White).Bold().FontSize(8);
                            row.RelativeItem(2).Text("Yazar").FontColor(Colors.White).Bold().FontSize(8);
                            row.RelativeItem(2).Text("E-posta").FontColor(Colors.White).Bold().FontSize(8);
                            row.ConstantItem(55).Text("Durum").FontColor(Colors.White).Bold().FontSize(8);
                            row.ConstantItem(52).Text("Tarih").FontColor(Colors.White).Bold().FontSize(8);
                        });

                        for (var i = 0; i < submissions.Count; i++)
                        {
                            var s = submissions[i];
                            var bg = i % 2 == 0 ? "#ffffff" : "#f8fafc";
                            var statusLabel = s.Status switch
                            {
                                SubmissionStatus.Accepted => "Kabul",
                                SubmissionStatus.Rejected => "Red",
                                SubmissionStatus.RevisionRequired => "Revizyon",
                                SubmissionStatus.Pending => "Beklemede",
                                SubmissionStatus.UnderReview => "İncelemede",
                                _ => s.Status.ToString()
                            };
                            var statusColor = s.Status switch
                            {
                                SubmissionStatus.Accepted => "#16a34a",
                                SubmissionStatus.Rejected => "#dc2626",
                                SubmissionStatus.RevisionRequired => "#d97706",
                                _ => "#6b7280"
                            };

                            col.Item().Background(bg).BorderBottom(1).BorderColor("#e5e7eb")
                                .PaddingVertical(4).PaddingHorizontal(6).Row(row =>
                            {
                                row.ConstantItem(22).Text((i + 1).ToString()).FontSize(8).FontColor("#6b7280");
                                row.RelativeItem(4).Text(s.Title ?? "").FontSize(8);
                                row.RelativeItem(2).Text(s.AuthorName ?? "").FontSize(8);
                                row.RelativeItem(2).Text(s.AuthorEmail ?? "").FontSize(7).FontColor("#6b7280");
                                row.ConstantItem(55).Text(statusLabel).FontSize(8).Bold().FontColor(statusColor);
                                row.ConstantItem(52).Text(s.CreatedDate.ToString("dd.MM.yyyy")).FontSize(8).FontColor("#6b7280");
                            });
                        }
                    });

                    page.Footer().PaddingHorizontal(6).Row(row =>
                    {
                        row.RelativeItem().Text($"AntAbstract — {conference.Title}")
                            .FontSize(7).FontColor("#9ca3af");
                        row.ConstantItem(80).AlignRight()
                            .Text(x =>
                            {
                                x.Span("Sayfa ").FontSize(7).FontColor("#9ca3af");
                                x.CurrentPageNumber().FontSize(7).FontColor("#9ca3af");
                                x.Span(" / ").FontSize(7).FontColor("#9ca3af");
                                x.TotalPages().FontSize(7).FontColor("#9ca3af");
                            });
                    });
                });
            }).GeneratePdf();

            var safeTitle = string.Join(
                "_",
                (conference.Title ?? "conference")
                    .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

            var fileName = $"rapor_{safeTitle}_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet("/{slug}/Admin/Reports/Referees")]
        public async Task<IActionResult> RefereeReport(
            string slug,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Reports" });
            }

            SetSelectedConferenceSession(conference);

            var stats = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => ra.Submission != null && ra.Submission.ConferenceId == conference.Id)
                .GroupBy(ra => ra.ReviewerId)
                .Select(g => new
                {
                    ReviewerId = g.Key,
                    TotalAssigned = g.Count(),
                    TotalCompleted = g.Count(ra => ra.Review != null),
                    AverageScore = g.Where(ra => ra.Review != null && ra.Review.Score > 0)
                                      .Select(ra => (double?)ra.Review!.Score)
                                      .Average()
                })
                .ToListAsync();

            var reviewerIds = stats.Select(s => s.ReviewerId).ToList();
            var reviewers = await _context.Users
                .AsNoTracking()
                .Where(u => reviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.University })
                .ToDictionaryAsync(u => u.Id);

            var rows = stats.Select(s =>
            {
                reviewers.TryGetValue(s.ReviewerId, out var rev);
                return new
                {
                    FullName = rev != null ? $"{rev.FirstName} {rev.LastName}".Trim() : s.ReviewerId,
                    Email = rev?.Email ?? "",
                    University = rev?.University ?? "",
                    s.TotalAssigned,
                    s.TotalCompleted,
                    Pending = s.TotalAssigned - s.TotalCompleted,
                    AverageScore = s.AverageScore.HasValue
                        ? Math.Round(s.AverageScore.Value, 1).ToString("F1")
                        : "-",
                    CompletionRate = s.TotalAssigned > 0
                        ? $"{(int)(s.TotalCompleted * 100.0 / s.TotalAssigned)}%"
                        : "0%"
                };
            }).OrderByDescending(r => r.TotalCompleted).ToList();

            ViewBag.Slug = slug;
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Rows = rows;

            return View("~/Areas/Admin/Views/Reports/RefereeReport.cshtml");
        }

        // ── Hakem Performans Export (Excel) ─────────────────────────────────────

        [HttpGet("/Admin/Reports/RefereeExport")]
        [HttpGet("/{slug}/Admin/Reports/RefereeExport")]
        public async Task<IActionResult> RefereeExport(
            string slug,
            Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);
            if (conference == null)
                return RedirectToAction(nameof(Index), new { slug });

            var stats = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => ra.Submission != null && ra.Submission.ConferenceId == conference.Id)
                .GroupBy(ra => ra.ReviewerId)
                .Select(g => new
                {
                    ReviewerId = g.Key,
                    TotalAssigned = g.Count(),
                    TotalCompleted = g.Count(ra => ra.Review != null),
                    Declined = g.Count(ra => ra.IsDeclined),
                    AverageScore = g.Where(ra => ra.Review != null && ra.Review.Score > 0)
                                      .Select(ra => (double?)ra.Review!.Score)
                                      .Average()
                })
                .ToListAsync();

            var reviewerIds = stats.Select(s => s.ReviewerId).ToList();
            var reviewers = await _context.Users
                .AsNoTracking()
                .Where(u => reviewerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.University })
                .ToDictionaryAsync(u => u.Id);

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Hakem Performans");

            ws.Cell(1, 1).Value = $"Hakem Performans Raporu — {conference.Title}";
            ws.Range(1, 1, 1, 7).Merge().Style.Font.SetBold().Font.SetFontSize(13);

            var headers = new[] { "Ad Soyad", "E-posta", "Kurum", "Atanan", "Tamamlanan", "Bekleyen", "Ort. Puan", "Tamamlanma %" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.SetBold();
            }

            int row = 4;
            foreach (var s in stats.OrderByDescending(x => x.TotalCompleted))
            {
                reviewers.TryGetValue(s.ReviewerId, out var rev);
                ws.Cell(row, 1).Value = rev != null ? $"{rev.FirstName} {rev.LastName}".Trim() : s.ReviewerId;
                ws.Cell(row, 2).Value = rev?.Email ?? "";
                ws.Cell(row, 3).Value = rev?.University ?? "";
                ws.Cell(row, 4).Value = s.TotalAssigned;
                ws.Cell(row, 5).Value = s.TotalCompleted;
                ws.Cell(row, 6).Value = s.TotalAssigned - s.TotalCompleted;
                ws.Cell(row, 7).Value = s.AverageScore.HasValue ? Math.Round(s.AverageScore.Value, 1) : 0;
                ws.Cell(row, 8).Value = s.TotalAssigned > 0
                    ? $"{(int)(s.TotalCompleted * 100.0 / s.TotalAssigned)}%"
                    : "0%";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new System.IO.MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"hakem-performans-{conference.Slug ?? "conf"}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }

        // ── Katılımcı Raporu (CSV) ──────────────────────────────────────────────

        [HttpGet("/Admin/Reports/ParticipantsExport")]
        [HttpGet("/{slug}/Admin/Reports/ParticipantsExport")]
        public async Task<IActionResult> ParticipantsExport(
            string slug,
            Guid? conferenceId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string format = "csv")
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);
            if (conference == null)
                return NotFound();

            var query = _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Where(r => r.ConferenceId == conference.Id);

            if (dateFrom.HasValue)
                query = query.Where(r => r.RegistrationDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                query = query.Where(r => r.RegistrationDate < dateTo.Value.Date.AddDays(1));

            var rows = await query.OrderBy(r => r.AppUser!.LastName).ToListAsync();

            if (format == "xlsx")
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Katılımcılar");
                var headers = new[] { "Ad", "Soyad", "E-posta", "Kurum", "Kayıt Tipi", "Ödeme Durumu", "Kayıt Tarihi" };
                for (var i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                }
                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i]; var u = r.AppUser; var row = i + 2;
                    ws.Cell(row, 1).Value = u?.FirstName ?? "";
                    ws.Cell(row, 2).Value = u?.LastName ?? "";
                    ws.Cell(row, 3).Value = u?.Email ?? "";
                    ws.Cell(row, 4).Value = u?.University ?? "";
                    ws.Cell(row, 5).Value = r.RegistrationType?.Name ?? "";
                    ws.Cell(row, 6).Value = r.IsPaid ? "Ödendi" : (r.ReceiptFilePath != null ? "Makbuz Bekleniyor" : "Ödenmedi");
                    ws.Cell(row, 7).Value = r.RegistrationDate.ToString("dd.MM.yyyy");
                }
                ws.Columns().AdjustToContents();
                using var ms = new System.IO.MemoryStream();
                wb.SaveAs(ms);
                return File(ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"katilimcilar-{conference.Slug}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ad,Soyad,E-posta,Kurum,Kayıt Tipi,Ödeme Durumu,Kayıt Tarihi");
            foreach (var r in rows)
            {
                var u = r.AppUser;
                sb.AppendLine(string.Join(",",
                    CsvEsc(u?.FirstName ?? ""),
                    CsvEsc(u?.LastName ?? ""),
                    CsvEsc(u?.Email ?? ""),
                    CsvEsc(u?.University ?? ""),
                    CsvEsc(r.RegistrationType?.Name ?? ""),
                    CsvEsc(r.IsPaid ? "Ödendi" : (r.ReceiptFilePath != null ? "Makbuz Bekleniyor" : "Ödenmedi")),
                    CsvEsc(r.RegistrationDate.ToString("dd.MM.yyyy"))));
            }
            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8",
                $"katilimcilar-{conference.Slug}-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // ── Gelir Raporu (CSV) ──────────────────────────────────────────────────

        [HttpGet("/Admin/Reports/RevenueExport")]
        [HttpGet("/{slug}/Admin/Reports/RevenueExport")]
        public async Task<IActionResult> RevenueExport(
            string slug,
            Guid? conferenceId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string format = "csv")
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);
            if (conference == null)
                return NotFound();

            var query = _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Where(r => r.ConferenceId == conference.Id && r.IsPaid);

            if (dateFrom.HasValue)
                query = query.Where(r => r.RegistrationDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                query = query.Where(r => r.RegistrationDate < dateTo.Value.Date.AddDays(1));

            var rows = await query.OrderByDescending(r => r.RegistrationDate).ToListAsync();
            var totalRevenue = rows.Sum(r => r.Amount);
            var currency = rows.FirstOrDefault()?.RegistrationType?.Currency ?? "TRY";

            if (format == "xlsx")
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Gelir Raporu");
                ws.Cell(1, 1).Value = $"Kongre: {conference.Title}";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(2, 1).Value = $"Toplam Gelir: {totalRevenue:N2} {currency}";
                ws.Cell(3, 1).Value = $"Ödenen Kayıt Sayısı: {rows.Count}";
                var headers = new[] { "Ad", "Soyad", "E-posta", "Kayıt Tipi", "Tutar", "Para Birimi", "Tarih" };
                for (var i = 0; i < headers.Length; i++)
                {
                    ws.Cell(5, i + 1).Value = headers[i];
                    ws.Cell(5, i + 1).Style.Font.Bold = true;
                }
                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i]; var u = r.AppUser; var row = i + 6;
                    ws.Cell(row, 1).Value = u?.FirstName ?? "";
                    ws.Cell(row, 2).Value = u?.LastName ?? "";
                    ws.Cell(row, 3).Value = u?.Email ?? "";
                    ws.Cell(row, 4).Value = r.RegistrationType?.Name ?? "";
                    ws.Cell(row, 5).Value = r.Amount;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 6).Value = r.RegistrationType?.Currency ?? "TRY";
                    ws.Cell(row, 7).Value = r.RegistrationDate.ToString("dd.MM.yyyy");
                }
                ws.Columns().AdjustToContents();
                using var ms = new System.IO.MemoryStream();
                wb.SaveAs(ms);
                return File(ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"gelir-raporu-{conference.Slug ?? "conf"}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Kongre: {conference.Title}");
            sb.AppendLine($"Toplam Gelir: {totalRevenue:N2} {currency}");
            sb.AppendLine($"Ödenen Kayıt Sayısı: {rows.Count}");
            sb.AppendLine("");
            sb.AppendLine("Ad,Soyad,E-posta,Kayıt Tipi,Tutar,Para Birimi,Tarih");
            foreach (var r in rows)
            {
                var u = r.AppUser;
                sb.AppendLine(string.Join(",",
                    CsvEsc(u?.FirstName ?? ""),
                    CsvEsc(u?.LastName ?? ""),
                    CsvEsc(u?.Email ?? ""),
                    CsvEsc(r.RegistrationType?.Name ?? ""),
                    CsvEsc(r.Amount.ToString("N2")),
                    CsvEsc(r.RegistrationType?.Currency ?? "TRY"),
                    CsvEsc(r.RegistrationDate.ToString("dd.MM.yyyy"))));
            }
            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8",
                $"gelir-raporu-{conference.Slug ?? "conf"}-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // ── Bildiri Özet Raporu ─────────────────────────────────────────────────

        [HttpGet("/Admin/Reports/SubmissionsExport")]
        [HttpGet("/{slug}/Admin/Reports/SubmissionsExport")]
        public async Task<IActionResult> SubmissionsExport(
            string slug,
            Guid? conferenceId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string format = "csv")
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);
            if (conference == null)
                return NotFound();

            var query = _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Include(s => s.ConferenceTopic)
                .Where(s => s.ConferenceId == conference.Id);

            if (dateFrom.HasValue)
                query = query.Where(s => s.CreatedDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                query = query.Where(s => s.CreatedDate < dateTo.Value.Date.AddDays(1));

            var rows = await query.OrderByDescending(s => s.CreatedDate).ToListAsync();

            if (format == "xlsx")
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Bildiriler");
                var headers = new[] { "Başlık", "Yazar", "Konu", "Sunum Türü", "Durum", "Gönderim Tarihi" };
                for (var i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                }
                for (var i = 0; i < rows.Count; i++)
                {
                    var s = rows[i]; var row = i + 2;
                    ws.Cell(row, 1).Value = s.Title ?? "";
                    ws.Cell(row, 2).Value = s.Author != null ? $"{s.Author.FirstName} {s.Author.LastName}".Trim() : "";
                    ws.Cell(row, 3).Value = s.ConferenceTopic?.Name ?? s.Topic ?? "";
                    ws.Cell(row, 4).Value = s.PresentationType ?? "";
                    ws.Cell(row, 5).Value = s.Status.ToString();
                    ws.Cell(row, 6).Value = s.CreatedDate.ToString("dd.MM.yyyy");
                }
                ws.Columns().AdjustToContents();
                using var ms = new System.IO.MemoryStream();
                wb.SaveAs(ms);
                return File(ms.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"bildiriler-{conference.Slug}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Başlık,Yazar,Konu,Sunum Türü,Durum,Gönderim Tarihi");
            foreach (var s in rows)
            {
                sb.AppendLine(string.Join(",",
                    CsvEsc(s.Title ?? ""),
                    CsvEsc(s.Author != null ? $"{s.Author.FirstName} {s.Author.LastName}".Trim() : ""),
                    CsvEsc(s.ConferenceTopic?.Name ?? s.Topic ?? ""),
                    CsvEsc(s.PresentationType ?? ""),
                    CsvEsc(s.Status.ToString()),
                    CsvEsc(s.CreatedDate.ToString("dd.MM.yyyy"))));
            }
            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8",
                $"bildiriler-{conference.Slug}-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private static string CsvEsc(string v)
        {
            // Excel formula injection koruması: =, +, -, @ ile başlayan
            // hücreler prefix tırnak eklenerek metin olarak yorumlanır.
            if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@')
                v = "'" + v;

            if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
                return $"\"{v.Replace("\"", "\"\"")}\"";
            return v;
        }

        [HttpGet("/{slug}/Admin/Reports/UnregisteredAuthors")]
        public async Task<IActionResult> UnregisteredAuthors(string slug, Guid? conferenceId = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T("Error_SelectConferenceFirst", "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Reports/UnregisteredAuthors" });
            }

            SetSelectedConferenceSession(conference);

            var confId = conference.Id;

            // Kabul edilen bildiriler: hiçbir yazarın bu kongreye ücretli kaydı yok
            var paidUserIds = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.ConferenceId == confId && r.IsPaid)
                .Select(r => r.AppUserId)
                .ToListAsync();

            var unregistered = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.SubmissionAuthors)
                .Where(s => s.ConferenceId == confId && s.Status == SubmissionStatus.Accepted)
                .Where(s => !s.SubmissionAuthors
                    .Where(a => a.AppUserId != null)
                    .Any(a => paidUserIds.Contains(a.AppUserId!)))
                .OrderBy(s => s.Title)
                .ToListAsync();

            ViewBag.Conference = conference;
            ViewBag.Slug = slug;
            ViewBag.PaidUserIds = paidUserIds;

            return View(unregistered);
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
