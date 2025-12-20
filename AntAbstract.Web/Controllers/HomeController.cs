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

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var registeredIds = new List<Guid>();
            var userRegistrationMap = new Dictionary<Guid, Guid>();
            var paidConferenceIds = new List<Guid>();

            if (user != null)
            {
                var userRegistrations = await _context.Registrations
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => new { r.ConferenceId, r.Id, r.IsPaid })
                    .ToListAsync();

                registeredIds = userRegistrations
                    .Select(r => r.ConferenceId)
                    .Distinct()
                    .ToList();

                userRegistrationMap = userRegistrations
                    .GroupBy(r => r.ConferenceId)
                    .ToDictionary(g => g.Key, g => g.First().Id);

                paidConferenceIds = userRegistrations
                    .Where(r => r.IsPaid)
                    .Select(r => r.ConferenceId)
                    .Distinct()
                    .ToList();
            }

            ViewBag.RegisteredConferenceIds = registeredIds;
            ViewBag.UserRegistrationIds = userRegistrationMap;
            ViewBag.PaidConferenceIds = paidConferenceIds;

            if (_tenantContext.Current != null)
            {
                var currentConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .Include(c => c.Registrations)
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

                if (currentConference == null) return NotFound("Kongre aktif deðil.");

                return View("ConferenceHome", currentConference);
            }

            var activeConferences = await _context.Conferences
                .Include(c => c.Tenant)
                .Include(c => c.Registrations)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            return View(activeConferences);
        }

        public async Task<IActionResult> Congresses()
        {
            var allCongresses = await _context.Conferences
                .Include(c => c.Tenant)
                .Include(c => c.Registrations)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);

            var registeredIds = new List<Guid>();
            var userRegistrationMap = new Dictionary<Guid, Guid>();
            var paidConferenceIds = new List<Guid>();

            if (user != null)
            {
                var userRegistrations = await _context.Registrations
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => new { r.ConferenceId, r.Id, r.IsPaid })
                    .ToListAsync();

                registeredIds = userRegistrations
                    .Select(r => r.ConferenceId)
                    .Distinct()
                    .ToList();

                userRegistrationMap = userRegistrations
                    .GroupBy(r => r.ConferenceId)
                    .ToDictionary(g => g.Key, g => g.First().Id);

                paidConferenceIds = userRegistrations
                    .Where(r => r.IsPaid)
                    .Select(r => r.ConferenceId)
                    .Distinct()
                    .ToList();
            }

            ViewBag.RegisteredConferenceIds = registeredIds;
            ViewBag.UserRegistrationIds = userRegistrationMap;
            ViewBag.PaidConferenceIds = paidConferenceIds;

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
