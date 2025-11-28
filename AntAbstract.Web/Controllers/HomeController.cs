using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity; // Bu gerekli
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

        // YENÝ EKLENEN: UserManager Tanýmý
        private readonly UserManager<AppUser> _userManager;

        // Constructor'ý güncelledik: userManager parametresi eklendi
        public HomeController(AppDbContext context, TenantContext tenantContext, UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager; // Atama yapýldý
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

                // Özel Landing Page
                return View("ConferenceHome", currentConference);
            }

            // 2. SENARYO: Ana Portal (Liste)
            var activeConferences = await _context.Conferences
                .Include(c => c.Tenant) // Slug için Tenant'ý dahil et
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            // --- KULLANICININ KAYITLI OLDUÐU KONGRELERÝ BULMA ---
            // Bu kýsým artýk _userManager sayesinde çalýþacak
            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                var registeredConferenceIds = await _context.Registrations
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId)
                    .ToListAsync();

                ViewBag.RegisteredConferenceIds = registeredConferenceIds;
            }
            else
            {
                ViewBag.RegisteredConferenceIds = new List<Guid>();
            }
            // ----------------------------------------------------

            return View(activeConferences);
        }

        // Diðer metodlarýnýz (Details, About, Contact vb.) aynen kalabilir

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        // Kongreler Sayfasý (Menüden gelen)
        public async Task<IActionResult> Congresses()
        {
            // Burada da ayný kayýt kontrolünü yapmak isterseniz yukarýdaki mantýðý buraya da eklemelisiniz.
            // Þimdilik sadece listeyi döndürelim.
            var allCongresses = await _context.Conferences
               .Include(c => c.Tenant)
               .OrderBy(c => c.StartDate)
               .ToListAsync();

            // Kullanýcý giriþ yapmýþsa kayýtlý olduðu kongreleri buraya da gönderebiliriz
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.RegisteredConferenceIds = await _context.Registrations
                   .Where(r => r.AppUserId == user.Id)
                   .Select(r => r.ConferenceId)
                   .ToListAsync();
            }

            return View(allCongresses);
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