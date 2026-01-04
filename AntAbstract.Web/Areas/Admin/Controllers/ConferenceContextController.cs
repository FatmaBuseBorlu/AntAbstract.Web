using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    public class ConferenceContextController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public ConferenceContextController(AppDbContext context, ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/SelectConference")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null, string? title = null, string? lead = null)
        {
            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedConf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConf?.Tenant?.Slug != null)
                {
                    SetConferenceSession(selectedConf);
                    var target = BuildTargetUrl(returnUrl, selectedConf.Tenant.Slug, selectedConf.Id);
                    return Redirect(target);
                }
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Kongre Seç" : title,
                Lead = string.IsNullOrWhiteSpace(lead) ? "Devam etmek için bir kongre seçin." : lead,
                PostUrl = "/Admin/SelectConference",
                SubmitText = "Devam Et",
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/SelectConference")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null, string? title = null, string? lead = null)
        {
            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(SelectConference), new { returnUrl, title, lead });
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            SetConferenceSession(conf);

            var target = BuildTargetUrl(returnUrl, conf.Tenant.Slug, conf.Id);
            return Redirect(target);
        }

        private void SetConferenceSession(Conference conf)
        {
            var slug = conf.Tenant?.Slug ?? "";
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conf.Title ?? "");

            var tenantId = conf.TenantId;
            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conf.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conf.Title ?? "");
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
        }

        private string BuildTargetUrl(string? returnUrl, string tenantSlug, Guid conferenceId)
        {
            var target = string.IsNullOrWhiteSpace(returnUrl) ? "/Admin/Dashboard" : returnUrl;

            if (!Url.IsLocalUrl(target))
                target = "/Admin/Dashboard";

            var parts = target.Split('?', 2);
            var path = parts[0];
            var qs = parts.Length == 2 ? "?" + parts[1] : "";

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2 && segments[1].Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                var adminTail = segments.Length > 2 ? string.Join('/', segments.Skip(2)) : "";
                path = string.IsNullOrWhiteSpace(adminTail) ? "/Admin" : "/Admin/" + adminTail;
            }

            if (path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
                path = $"/{tenantSlug}{path}";

            target = path + qs;

            if (!target.Contains("conferenceId=", StringComparison.OrdinalIgnoreCase))
            {
                var sep = target.Contains("?") ? "&" : "?";
                target = $"{target}{sep}conferenceId={conferenceId}";
            }

            return target;
        }
    }
}
