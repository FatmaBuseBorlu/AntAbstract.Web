using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System; // Guid için gerekli
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

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
            // 1. SENARYO: Kongre Sitesi (Tenant)
            if (_tenantContext.Current != null)
            {
                var currentConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

                if (currentConference == null) return NotFound("Kongre aktif deðil.");
                return View("ConferenceHome", currentConference);
            }

            // 2. SENARYO: Ana Portal (Liste)
            var activeConferences = await _context.Conferences
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            // --- YENÝ EKLENEN KISIM: Kullanýcýnýn kayýtlý olduðu kongreleri bul ---
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // Kullanýcýnýn kayýt olduðu ConferenceId'lerini bir liste olarak alýyoruz
                var registeredConferenceIds = await _context.Registrations
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId)
                    .ToListAsync();

                // Bu listeyi View'a taþýyoruz
                ViewBag.RegisteredConferenceIds = registeredConferenceIds;
            }
            else
            {
                ViewBag.RegisteredConferenceIds = new List<Guid>(); // Boþ liste
            }
            // ----------------------------------------------------------------------

            return View(activeConferences);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public async Task<IActionResult> Congresses()
        {
            // Tüm kongreleri tarihe göre (en yakýndan uzaða) sýralayýp getiriyoruz
            var allCongresses = await _context.Conferences
                .Include(c => c.Tenant)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            return View(allCongresses);
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