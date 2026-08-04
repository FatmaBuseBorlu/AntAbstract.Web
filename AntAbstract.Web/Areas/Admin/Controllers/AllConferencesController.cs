using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.WebUI.Models.ViewModels.Admin.AllConferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
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

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await FillTenantsAsync();
            return View(new Conference { StartDate = DateTime.Today, EndDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Conference conference)
        {
            // Navigasyon alanları formdan gelmez. Nullable açık olduğu için MVC
            // bunları zorunlu sayıyor ve doğrulama hiç geçmiyordu.
            ModelState.Remove(nameof(Conference.Tenant));
            ModelState.Remove(nameof(Conference.Slug));
            ModelState.Remove(nameof(Conference.Title));

            conference.Title = conference.Title?.Trim() ?? "";
            conference.Slug = conference.Slug?.Trim().ToLowerInvariant() ?? "";

            if (conference.TenantId == Guid.Empty)
            {
                ModelState.AddModelError(
                    nameof(Conference.TenantId), "Kurum seçmelisiniz.");
            }
            else if (!await _context.Tenants.AnyAsync(t => t.Id == conference.TenantId))
            {
                ModelState.AddModelError(
                    nameof(Conference.TenantId), "Seçilen kurum bulunamadı.");
            }

            if (string.IsNullOrWhiteSpace(conference.Title))
            {
                ModelState.AddModelError(
                    nameof(Conference.Title), "Kongre adı zorunludur.");
            }

            if (string.IsNullOrWhiteSpace(conference.Slug))
            {
                ModelState.AddModelError(
                    nameof(Conference.Slug), "Slug zorunludur.");
            }
            else if (!SlugPattern.IsMatch(conference.Slug))
            {
                ModelState.AddModelError(
                    nameof(Conference.Slug),
                    "Slug yalnızca küçük harf, rakam ve tire içerebilir.");
            }
            else if (await _context.Conferences
                         .IgnoreQueryFilters()
                         .AnyAsync(c => c.Slug == conference.Slug))
            {
                // Slug benzersiz indeksli; kontrol edilmezse kayıt 500 ile düşer.
                ModelState.AddModelError(
                    nameof(Conference.Slug),
                    "Bu slug başka bir kongrede kullanılıyor.");
            }

            if (conference.EndDate < conference.StartDate)
            {
                ModelState.AddModelError(
                    nameof(Conference.EndDate),
                    "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            }

            if (!ModelState.IsValid)
            {
                await FillTenantsAsync(conference.TenantId);
                return View(conference);
            }

            conference.Id = Guid.NewGuid();
            _context.Conferences.Add(conference);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kongre başarıyla oluşturuldu.";
            return RedirectToAction("Index");
        }

        private static readonly Regex SlugPattern =
            new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

        private async Task FillTenantsAsync(Guid? selectedId = null)
        {
            ViewBag.Tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name,
                    Selected = selectedId.HasValue && x.Id == selectedId.Value
                })
                .ToListAsync();
        }
    }
}