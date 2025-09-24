using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TenantContext _tc;

        // TEK constructor: Logger + TenantContext birlikte
        public HomeController(ILogger<HomeController> logger, TenantContext tc)
        {
            _logger = logger;
            _tc = tc;
        }

        public IActionResult Index()
        {
            ViewBag.TenantSlug = _tc.Current?.Slug;
            ViewBag.TenantName = _tc.Current?.Name;
            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
