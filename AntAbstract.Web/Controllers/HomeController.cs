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
            if (_tenantContext.Current != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            var allTenants = await _context.Tenants.ToListAsync();
            return View("TenantSelection", allTenants);
        }

        //  YENÝ EKLENEN METOT
        public async Task<IActionResult> Program()
        {
            // O anki kongreye ait Conference nesnesini bul.
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                // Henüz bir etkinlik tanýmlanmamýþsa hata mesajý göster.
                ViewBag.ErrorMessage = "Bu kongre için henüz bir program yayýnlanmamýþtýr.";
                return View(new List<Session>());
            }

            // Bu konferansa ait tüm oturumlarý, tarih sýrasýna göre, içindeki özet ve yazar bilgileriyle birlikte çek.
            var sessions = await _context.Sessions
                .Where(s => s.ConferenceId == conference.Id && s.Submissions.Any()) // Sadece içinde sunum olan oturumlarý getir
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