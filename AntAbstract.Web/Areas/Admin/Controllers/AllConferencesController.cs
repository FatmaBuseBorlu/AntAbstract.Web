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
        public async Task<IActionResult> Index(string? search, Guid? tenantId)
        {
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

            var conferences = await query
                .OrderByDescending(x => x.StartDate)
                .Select(x => new AllConferenceListItemViewModel
                {
                    Id = x.Id,
                    Title = x.Title ?? "",
                    Slug = x.Slug,

                    TenantId = x.TenantId,
                    TenantName = x.Tenant != null ? x.Tenant.Name : "-",
                    TenantSlug = x.Tenant != null ? x.Tenant.Slug : null,

                    City = x.City,
                    Country = x.Country,
                    Venue = x.Venue,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,

                    SubmissionCount = _context.Submissions.Count(s => s.ConferenceId == x.Id),
                    RegistrationCount = _context.Registrations.Count(r => r.ConferenceId == x.Id)
                })
                .ToListAsync();

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