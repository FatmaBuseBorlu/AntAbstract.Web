using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
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

        [HttpGet("/Admin/AuditLogs")]
        [HttpGet("/{slug}/Admin/AuditLogs")]
        public async Task<IActionResult> Index(
            string? slug,
            Guid? conferenceId,
            string? category = null,
            string? search = null,
            int page = 1)
        {
            const int PageSize = 50;

            var accessibleConferences = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            // Kongreye göre filtrele
            var confIds = await accessibleConferences.Select(c => c.Id).ToListAsync();

            var query = _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.ConferenceId == null || confIds.Contains(a.ConferenceId.Value));

            if (conferenceId.HasValue && conferenceId != Guid.Empty)
                query = query.Where(a => a.ConferenceId == conferenceId);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(a => a.Category == category);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(a =>
                    (a.UserName != null && a.UserName.Contains(s)) ||
                    (a.Action.Contains(s)) ||
                    (a.Description != null && a.Description.Contains(s)) ||
                    (a.EntityId != null && a.EntityId.Contains(s)));
            }

            var total = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Mevcut kategoriler (filtre için)
            var categories = await _context.AuditLogs
                .AsNoTracking()
                .Select(a => a.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Category = category;
            ViewBag.Search = search;
            ViewBag.ConferenceId = conferenceId;
            ViewBag.Slug = slug;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
            ViewBag.Total = total;

            return View(logs);
        }
    }
}
