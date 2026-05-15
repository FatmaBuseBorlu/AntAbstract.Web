using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ConferenceContextController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<ConferenceContextController> _localizer;

        public ConferenceContextController(
            AppDbContext context,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<ConferenceContextController> localizer)
        {
            _context = context;
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

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!isAdmin)
            {
                if (user?.TenantId == null)
                {
                    query = query.Where(c => false);
                }
                else
                {
                    query = query.Where(c => c.TenantId == user.TenantId.Value);
                }
            }

            return query;
        }

        [HttpGet("/Admin/SelectConference")]
        public async Task<IActionResult> SelectConference(
            string? returnUrl = null,
            string? title = null,
            string? lead = null)
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConf = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConf?.Tenant?.Slug != null)
                {
                    SetConferenceSession(selectedConf);

                    var target = BuildTargetUrl(
                        returnUrl,
                        selectedConf.Tenant.Slug,
                        selectedConf.Id);

                    return Redirect(target);
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = string.IsNullOrWhiteSpace(title)
                    ? T("SelectConference_Title", "Kongre Seç")
                    : title,

                Lead = string.IsNullOrWhiteSpace(lead)
                    ? T("SelectConference_Lead", "İşleme devam etmek için kongre seçiniz.")
                    : lead,

                PostUrl = "/Admin/SelectConference",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/SelectConference")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(
            Guid conferenceId,
            string? returnUrl = null,
            string? title = null,
            string? lead = null)
        {
            var query = await GetAccessibleConferenceQueryAsync();

            var conf = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null ||
                conf.Tenant == null ||
                string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference), new
                {
                    returnUrl,
                    title,
                    lead
                });
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            SetConferenceSession(conf);

            var target = BuildTargetUrl(returnUrl, conf.Tenant.Slug, conf.Id);

            return Redirect(target);
        }

        private void SetConferenceSession(Conference conf)
        {
            var slug = conf.Tenant?.Slug ?? "";
            var tenantId = conf.TenantId;

            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");
            HttpContext.Session.SetString("SelectedConferenceId", conf.Id.ToString());

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conf.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conf.Title ?? "");
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
        }

        private string BuildTargetUrl(string? returnUrl, string tenantSlug, Guid conferenceId)
        {
            var target = string.IsNullOrWhiteSpace(returnUrl)
                ? "/Admin/Dashboard"
                : returnUrl;

            if (!Url.IsLocalUrl(target))
            {
                target = "/Admin/Dashboard";
            }

            var parts = target.Split('?', 2);
            var path = parts[0];
            var queryString = parts.Length == 2 ? "?" + parts[1] : "";

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2 &&
                segments[1].Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                var adminTail = segments.Length > 2
                    ? string.Join('/', segments.Skip(2))
                    : "";

                path = string.IsNullOrWhiteSpace(adminTail)
                    ? "/Admin"
                    : "/Admin/" + adminTail;
            }

            if (path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
            {
                path = $"/{tenantSlug}{path}";
            }

            target = path + queryString;

            if (!target.Contains("conferenceId=", StringComparison.OrdinalIgnoreCase))
            {
                var separator = target.Contains("?") ? "&" : "?";
                target = $"{target}{separator}conferenceId={conferenceId}";
            }

            return target;
        }
    }
}