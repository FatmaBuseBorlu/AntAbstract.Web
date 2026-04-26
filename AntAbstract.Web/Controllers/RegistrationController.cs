using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    [Route("{slug}/registration")]
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;
        private readonly IStringLocalizer<RegistrationController> _localizer;

        public RegistrationController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            IStringLocalizer<RegistrationController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _localizer = localizer;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = RouteData.Values["slug"]?.ToString();

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == slug || (c.Tenant != null && c.Tenant.Slug == slug));

            if (conference == null)
            {
                TempData["ErrorMessage"] = _localizer["ConferenceNotFound"];
                return RedirectToAction("Index", "Home");
            }

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                if (existingRegistration.IsPaid)
                    TempData["SuccessMessage"] = _localizer["AlreadyRegisteredAndPaid"];
                else
                    TempData["InfoMessage"] = _localizer["ExistingRegistrationRedirectedToPayment"];

                return RedirectToAction("Index", "Payment", new { slug = slug, id = existingRegistration.Id });
            }

            var ticketTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(rt => rt.ConferenceId == conference.Id && rt.IsActive)
                .OrderBy(rt => rt.Price)
                .ToListAsync();

            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;

            return View(ticketTypes);
        }

        [HttpGet("checkout/{typeId:guid}")]
        public async Task<IActionResult> Checkout(Guid typeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = RouteData.Values["slug"]?.ToString();

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                .FirstOrDefaultAsync(rt => rt.Id == typeId);

            if (ticketType == null || !ticketType.IsActive || (ticketType.Deadline.HasValue && ticketType.Deadline.Value <= DateTime.UtcNow))
            {
                TempData["ErrorMessage"] = _localizer["InvalidOrExpiredTicket"];
                return RedirectToAction(nameof(Index), new { slug = slug });
            }

            var exists = await _context.Registrations.AnyAsync(r => r.ConferenceId == ticketType.ConferenceId && r.AppUserId == user.Id);
            if (exists)
                return RedirectToAction(nameof(Index), new { slug = slug });

            ViewBag.Ticket = ticketType;
            ViewBag.User = user;
            ViewBag.Slug = slug;

            return View(new Registration { RegistrationTypeId = typeId });
        }

        [HttpPost("checkout/{typeId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutPost(Guid typeId, string BillingName, string TaxOffice, string TaxNumber, string BillingAddress)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = RouteData.Values["slug"]?.ToString();

            var ticketType = await _context.RegistrationTypes.FindAsync(typeId);
            if (ticketType == null) return NotFound();

            var exists = await _context.Registrations.AnyAsync(r => r.ConferenceId == ticketType.ConferenceId && r.AppUserId == user.Id);
            if (exists)
            {
                var existingId = await _context.Registrations
                    .Where(r => r.ConferenceId == ticketType.ConferenceId && r.AppUserId == user.Id)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                return RedirectToAction("Index", "Payment", new { slug = slug, id = existingId });
            }

            var newRegistration = new Registration
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = ticketType.ConferenceId,
                RegistrationTypeId = ticketType.Id,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,
                Amount = ticketType.Price,
                BillingName = BillingName,
                TaxOffice = TaxOffice,
                TaxNumber = TaxNumber,
                BillingAddress = BillingAddress
            };

            _context.Registrations.Add(newRegistration);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["RegistrationSuccessCompletePayment"];

            return RedirectToAction("Index", "Payment", new { slug = slug, id = newRegistration.Id });
        }
    }
}