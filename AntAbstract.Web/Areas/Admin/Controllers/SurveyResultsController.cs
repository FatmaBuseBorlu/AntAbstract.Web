using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class SurveyResultsController : Controller
    {
        // Kurucu metoda dokunmadan çeviri: mesajlar eskiden doğrudan Türkçe
        // yazılıydı, İngilizce seçili kullanıcıya da Türkçe dönüyorlardı.
        private string T(string key, string fallback)
        {
            var value = HttpContext?.RequestServices
                .GetService<IStringLocalizer<SurveyResultsController>>()?[key];

            return value == null || value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private readonly AppDbContext _context;
        private readonly IAdminTenantAccessService _tenantAccess;

        public SurveyResultsController(AppDbContext context, IAdminTenantAccessService tenantAccess)
        {
            _context = context;
            _tenantAccess = tenantAccess;
        }

        private async Task<Conference?> GetConferenceAsync(string slug, Guid? conferenceId)
        {
            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                var c = await _context.Conferences.Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == conferenceId.Value);
                if (c != null)
                {
                    return c;
                }
            }

            return await _context.Conferences.Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Slug == slug || (x.Tenant != null && x.Tenant.Slug == slug));
        }

        [HttpGet("/{slug}/Admin/SurveyResults")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId)
        {
            var conference = await GetConferenceAsync(slug, conferenceId);
            if (conference == null)
            {
                TempData["ErrorMessage"] = T("Msg_KongreBulunamadi", "Kongre bulunamadı.");
                return Redirect($"/{slug}/Admin/Reports");
            }

            ViewBag.Slug = slug;
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;

            var answers = await _context.SurveyAnswers
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.Submission)
                .Where(a => a.ConferenceId == conference.Id)
                .OrderByDescending(a => a.SubmittedAt)
                .ToListAsync();

            return View(answers);
        }
    }
}
