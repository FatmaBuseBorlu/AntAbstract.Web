using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using AntAbstract.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;
using System; // Guid için gerekli

namespace AntAbstract.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public HomeController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Eðer bir alt sitedeysek (Örn: antabstract.com/vet2025)
            if (_tenantContext.Current != null)
            {
                // O kongrenin kendi ana sayfasýna veya Dashboard'una yönlendir
                return RedirectToAction("Index", "Dashboard");
            }

            // 2. Ana Portaldayýz (www.antabstract.com)
            // "TenantSelection" sayfasýna kongre listesini gönderiyoruz.

            var activeConferences = await _context.Conferences
                .Include(c => c.Tenant) // Link oluþturmak için Tenant bilgisi lazým
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            // DÝKKAT: View adý "TenantSelection", gönderilen model "activeConferences"
            return View("TenantSelection", activeConferences);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            if (id == Guid.Empty) return NotFound();

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conference == null)
            {
                return NotFound("Kongre bulunamadý.");
            }

            return View(conference);
        }
        // ... Program, Privacy, Error ve SetLanguage metodlarýnýz ayný kalacak ...

        public async Task<IActionResult> Program()
        {
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Bu kongre için henüz bir program yayýnlanmamýþtýr.";
                return View(new List<Session>());
            }

            var sessions = await _context.Sessions
                .Where(s => s.ConferenceId == conference.Id && s.Submissions.Any())
                .Include(s => s.Submissions)
                .ThenInclude(sub => sub.Author)
                .OrderBy(s => s.SessionDate)
                .ToListAsync();

            return View(sessions);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }
    }
}