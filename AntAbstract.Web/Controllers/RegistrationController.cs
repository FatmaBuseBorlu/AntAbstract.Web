using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

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

        private string GetSlug()
        {
            return RouteData.Values["slug"]?.ToString()
                   ?? _tenantContext.Current?.Slug
                   ?? HttpContext.Session.GetString("SelectedConferenceSlug")
                   ?? "";
        }

        private void SetSelectedConferenceSession(Conference conference, string slug)
        {
            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
        }

        private async Task<Conference?> GetConferenceBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var slug = GetSlug();

            var conference = await GetConferenceBySlugAsync(slug);

            if (conference == null)
            {
                TempData["ErrorMessage"] = _localizer["ConferenceNotFound"];
                return RedirectToAction("Index", "Home");
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            var existingRegistration = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == conference.Id &&
                    r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                if (existingRegistration.IsPaid)
                {
                    TempData["SuccessMessage"] = _localizer["AlreadyRegisteredAndPaid"];
                }
                else
                {
                    TempData["InfoMessage"] = _localizer["ExistingRegistrationRedirectedToPayment"];
                }

                return RedirectToAction(
                    "Index",
                    "Payment",
                    new
                    {
                        slug = canonicalSlug,
                        id = existingRegistration.Id
                    });
            }

            var ticketTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(rt =>
                    rt.ConferenceId == conference.Id &&
                    rt.IsActive)
                .OrderBy(rt => rt.Price)
                .ToListAsync();

            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = canonicalSlug;

            return View(ticketTypes);
        }

        [HttpGet("join")]
        public async Task<IActionResult> Join()
        {
            return await Index();
        }

        [HttpGet("checkout/{typeId:guid}")]
        public async Task<IActionResult> Checkout(Guid typeId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var slug = GetSlug();

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(rt => rt.Id == typeId);

            if (ticketType == null)
            {
                TempData["ErrorMessage"] = _localizer["InvalidOrExpiredTicket"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            if (!ticketType.IsActive)
            {
                TempData["ErrorMessage"] = _localizer["InvalidOrExpiredTicket"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            if (ticketType.Deadline.HasValue && ticketType.Deadline.Value <= DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = _localizer["InvalidOrExpiredTicket"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            var conference = ticketType.Conference;

            if (conference == null)
            {
                TempData["ErrorMessage"] = _localizer["ConferenceNotFound"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            var exists = await _context.Registrations
                .AnyAsync(r =>
                    r.ConferenceId == ticketType.ConferenceId &&
                    r.AppUserId == user.Id);

            if (exists)
            {
                return RedirectToAction(nameof(Index), new { slug = canonicalSlug });
            }

            ViewBag.Ticket = ticketType;
            ViewBag.User = user;
            ViewBag.Slug = canonicalSlug;

            return View(new Registration
            {
                RegistrationTypeId = typeId,
                ConferenceId = ticketType.ConferenceId,
                AppUserId = user.Id,
                Amount = ticketType.Price,
                IsPaid = false,
                RegistrationDate = DateTime.UtcNow
            });
        }

        [HttpPost("checkout/{typeId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutPost(
            Guid typeId,
            string BillingName,
            string TaxOffice,
            string TaxNumber,
            string BillingAddress)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var slug = GetSlug();

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(rt => rt.Id == typeId);

            if (ticketType == null)
            {
                TempData["ErrorMessage"] = _localizer["InvalidOrExpiredTicket"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            if (!ticketType.IsActive)
            {
                TempData["ErrorMessage"] = _localizer["InvalidOrExpiredTicket"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            if (ticketType.Deadline.HasValue && ticketType.Deadline.Value <= DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = _localizer["InvalidOrExpiredTicket"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            var conference = ticketType.Conference;

            if (conference == null)
            {
                TempData["ErrorMessage"] = _localizer["ConferenceNotFound"];
                return RedirectToAction(nameof(Index), new { slug });
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == ticketType.ConferenceId &&
                    r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                return RedirectToAction(
                    "Index",
                    "Payment",
                    new
                    {
                        slug = canonicalSlug,
                        id = existingRegistration.Id
                    });
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

            return RedirectToAction(
                "Index",
                "Payment",
                new
                {
                    slug = canonicalSlug,
                    id = newRegistration.Id
                });
        }
    }
}