using AntAbstract.Infrastructure.Context;
using AntAbstract.WebUI.Models.ViewModels.Admin.AllConferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    [Route("Admin/AllConferences/{action=Index}/{id?}")]
    public class AllConferencesController : Controller
    {
        private readonly AppDbContext _context;

        public AllConferencesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, Guid? tenantId, int page = 1)
        {
            const int pageSize = 50;
            if (page < 1) page = 1;

            var query = _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();

                query = query.Where(x =>
                    x.Title.Contains(keyword) ||
                    (x.Slug != null && x.Slug.Contains(keyword)) ||
                    (x.City != null && x.City.Contains(keyword)) ||
                    (x.Country != null && x.Country.Contains(keyword)) ||
                    (x.Tenant != null && x.Tenant.Name.Contains(keyword)) ||
                    (x.Tenant != null && x.Tenant.Slug != null && x.Tenant.Slug.Contains(keyword)));
            }

            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                query = query.Where(x => x.TenantId == tenantId.Value);
            }

            var ordered = query.OrderByDescending(x => x.StartDate);
            var totalCount = await ordered.CountAsync();

            // Sayfalanmış konferans listesi — correlated subquery yok
            var raw = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Slug,
                    x.TenantId,
                    TenantName = x.Tenant != null ? x.Tenant.Name : "-",
                    TenantSlug = x.Tenant != null ? x.Tenant.Slug : null,
                    x.City,
                    x.Country,
                    x.Venue,
                    x.StartDate,
                    x.EndDate
                })
                .ToListAsync();

            // Submission ve Registration sayılarını tek sorguda çek (N+1 yok)
            var confIds = raw.Select(x => x.Id).ToList();

            var submissionCounts = await _context.Submissions
                .AsNoTracking()
                .Where(s => confIds.Contains(s.ConferenceId))
                .GroupBy(s => s.ConferenceId)
                .Select(g => new { ConferenceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ConferenceId, g => g.Count);

            var registrationCounts = await _context.Registrations
                .AsNoTracking()
                .Where(r => confIds.Contains(r.ConferenceId))
                .GroupBy(r => r.ConferenceId)
                .Select(g => new { ConferenceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ConferenceId, g => g.Count);

            var conferences = raw.Select(x => new AllConferenceListItemViewModel
            {
                Id = x.Id,
                Title = x.Title ?? "",
                Slug = x.Slug,
                TenantId = x.TenantId,
                TenantName = x.TenantName,
                TenantSlug = x.TenantSlug,
                City = x.City,
                Country = x.Country,
                Venue = x.Venue,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                SubmissionCount = submissionCounts.GetValueOrDefault(x.Id),
                RegistrationCount = registrationCounts.GetValueOrDefault(x.Id)
            }).ToList();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.Search = search;
            ViewBag.TenantId = tenantId;

            ViewBag.Tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name,
                    Selected = tenantId.HasValue && tenantId.Value == x.Id
                })
                .ToListAsync();

            return View(conferences);
        }
    }
}