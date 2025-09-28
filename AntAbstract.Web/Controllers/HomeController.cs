using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using AntAbstract.Domain.Entities; 
using Microsoft.EntityFrameworkCore; 
using System.Threading.Tasks; 


namespace AntAbstract.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context; // DbContext'i ekledik
        private readonly TenantContext _tenantContext;

        public HomeController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> Index()
        {
            // Eðer URL'de bir kongre adý VARSA (/icc2025 gibi),
            // kullanýcýyý o kongrenin kiþisel paneline (Dashboard) yönlendir.
            if (_tenantContext.Current != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            // Eðer URL'de bir kongre adý YOKSA (/), tüm kongreleri listeleyen
            // seçim sayfasýný göster.
            var allTenants = await _context.Tenants.ToListAsync();
            return View("TenantSelection", allTenants);
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
    }
}
