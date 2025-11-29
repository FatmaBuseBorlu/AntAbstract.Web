using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;
using AntAbstract.Web.Models;

namespace AntAbstract.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(AppDbContext context, TenantContext tenantContext, UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
        }

        // 1. ANA SAYFA
        public async Task<IActionResult> Index()
        {
            // --- KULLANICININ KAYIT VE ÖDEME DURUMUNU ÇEK ---
            var user = await _userManager.GetUserAsync(User);

            // Sözlük: <KongreID, ÖdendiMi>
            var registrationStatus = new Dictionary<Guid, bool>();

            if (user != null)
            {
                registrationStatus = await _context.Registrations
                    .Where(r => r.AppUserId == user.Id)
                    .ToDictionaryAsync(r => r.ConferenceId, r => r.IsPaid);
            }

            // View'a bu sözlüðü gönderiyoruz (Kritik Güncelleme)
            ViewBag.RegistrationStatus = registrationStatus;
            // -----------------------------------------------

            // SENARYO A: Kongre Sitesi (Tenant)
            if (_tenantContext.Current != null)
            {
                var currentConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

                if (currentConference == null) return NotFound("Kongre aktif deðil.");

                return View("ConferenceHome", currentConference);
            }

            // SENARYO B: Ana Portal (Liste)
            var activeConferences = await _context.Conferences
                .Include(c => c.Tenant)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            return View(activeConferences);
        }

        // 2. KONGRELER LÝSTESÝ SAYFASI
        public async Task<IActionResult> Congresses()
        {
            var allCongresses = await _context.Conferences
               .Include(c => c.Tenant)
               .OrderBy(c => c.StartDate)
               .ToListAsync();

            // --- KAYIT DURUMUNU BURADA DA ÇEKÝYORUZ ---
            var user = await _userManager.GetUserAsync(User);
            var registrationStatus = new Dictionary<Guid, bool>();

            if (user != null)
            {
                registrationStatus = await _context.Registrations
                   .Where(r => r.AppUserId == user.Id)
                   .ToDictionaryAsync(r => r.ConferenceId, r => r.IsPaid);
            }
            ViewBag.RegistrationStatus = registrationStatus;
            // -------------------------------------------

            return View(allCongresses);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}