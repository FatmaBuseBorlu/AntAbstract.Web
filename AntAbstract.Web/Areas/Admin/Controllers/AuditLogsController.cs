using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class AuditLogsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAdminTenantAccessService _tenantAccess;

        public AuditLogsController(AppDbContext context, IAdminTenantAccessService tenantAccess)
        {
            _context = context;
            _tenantAccess = tenantAccess;
        }

        // ── Index ───────────────────────────────────────────────────────────────

        [HttpGet("/Admin/AuditLogs")]
        [HttpGet("/{slug}/Admin/AuditLogs")]
        public async Task<IActionResult> Index(
            string? slug,
            Guid? conferenceId,
            string? category = null,
            string? search = null,
            string? userId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? action = null,
            int page = 1)
        {
            const int PageSize = 50;

            var query = await BuildQueryAsync(conferenceId, category, search, userId, dateFrom, dateTo, action);

            var total = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Filtre yardımcıları
            var accessibleConferences = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);
            var conferences = await accessibleConferences
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            var categories = await _context.AuditLogs
                .AsNoTracking()
                .Select(a => a.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Categories   = categories;
            ViewBag.Conferences  = conferences;
            ViewBag.Category     = category;
            ViewBag.Search       = search;
            ViewBag.UserId       = userId;
            ViewBag.Action       = action;
            ViewBag.DateFrom     = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo       = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.ConferenceId = conferenceId;
            ViewBag.Slug         = slug;
            ViewBag.Page         = page;
            ViewBag.TotalPages   = (int)Math.Ceiling(total / (double)PageSize);
            ViewBag.Total        = total;

            return View(logs);
        }

        // ── CSV Export ──────────────────────────────────────────────────────────

        [HttpGet("/Admin/AuditLogs/Export")]
        [HttpGet("/{slug}/Admin/AuditLogs/Export")]
        public async Task<IActionResult> Export(
            string? slug,
            Guid? conferenceId,
            string? category = null,
            string? search = null,
            string? userId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? action = null)
        {
            var query = await BuildQueryAsync(conferenceId, category, search, userId, dateFrom, dateTo, action);

            // Export'ta maksimum 10.000 satır — daha fazlası için zaman aralığı daraltılmalı
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(10_000)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Tarih,Kategori,İşlem,Kullanıcı,IP,Açıklama,KonferansId,EntityType,EntityId");

            foreach (var log in logs)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                    CsvEscape(log.Category),
                    CsvEscape(log.Action),
                    CsvEscape(log.UserName ?? ""),
                    CsvEscape(log.IpAddress ?? ""),
                    CsvEscape(log.Description ?? ""),
                    CsvEscape(log.ConferenceId?.ToString() ?? ""),
                    CsvEscape(log.EntityType ?? ""),
                    CsvEscape(log.EntityId ?? "")));
            }

            var fileName = $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // ── Yardımcılar ─────────────────────────────────────────────────────────

        private async Task<IQueryable<AuditLog>> BuildQueryAsync(
            Guid? conferenceId,
            string? category,
            string? search,
            string? userId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? action)
        {
            var accessibleConferences = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);
            var confIds = await accessibleConferences.Select(c => c.Id).ToListAsync();

            var q = _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.ConferenceId == null || confIds.Contains(a.ConferenceId.Value));

            if (conferenceId.HasValue && conferenceId != Guid.Empty)
                q = q.Where(a => a.ConferenceId == conferenceId);

            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(a => a.Category == category);

            if (!string.IsNullOrWhiteSpace(action))
                q = q.Where(a => a.Action == action);

            if (!string.IsNullOrWhiteSpace(userId))
                q = q.Where(a => a.UserId == userId);

            if (dateFrom.HasValue)
                q = q.Where(a => a.CreatedAt >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                q = q.Where(a => a.CreatedAt < dateTo.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(a =>
                    (a.UserName != null && a.UserName.Contains(s)) ||
                    a.Action.Contains(s) ||
                    (a.Description != null && a.Description.Contains(s)) ||
                    (a.EntityId != null && a.EntityId.Contains(s)));
            }

            return q;
        }

        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
