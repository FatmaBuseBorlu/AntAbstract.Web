using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    [Route("{slug}/Payment")]
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;
        private readonly INotificationService _notificationService;
        private readonly IStringLocalizer<PaymentController> _localizer;

        public PaymentController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            INotificationService notificationService,
            IStringLocalizer<PaymentController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _notificationService = notificationService;
            _localizer = localizer;
        }

        #region Helper Methods

        private string GetSlug()
        {
            return RouteData.Values["slug"]?.ToString()
                   ?? _tenantContext.Current?.Slug
                   ?? HttpContext.Session.GetString("SelectedConferenceSlug")
                   ?? "";
        }

        private Guid? GetSelectedConferenceId()
        {
            string? confIdStr = null;

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                confIdStr = HttpContext.Session.GetString(tenantKey);
            }

            confIdStr ??= HttpContext.Session.GetString("SelectedConferenceId");
            return Guid.TryParse(confIdStr, out var parsedId) ? parsedId : null;
        }

        private IActionResult RedirectToConferencePicker(string slug, string returnUrl, string? message = null)
        {
            if (!string.IsNullOrWhiteSpace(message))
                TempData["ErrorMessage"] = message;

            var url = string.IsNullOrWhiteSpace(slug)
                ? $"/Dashboard/MyConferences?returnUrl={Uri.EscapeDataString(returnUrl)}"
                : $"/{slug}/Dashboard/MyConferences?returnUrl={Uri.EscapeDataString(returnUrl)}";

            return Redirect(url);
        }

        private static bool SlugMatches(Conference? c, string slug)
        {
            if (c == null || string.IsNullOrWhiteSpace(slug))
                return false;

            if (!string.IsNullOrWhiteSpace(c.Slug) &&
                string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return true;

            if (c.Tenant != null &&
                !string.IsNullOrWhiteSpace(c.Tenant.Slug) &&
                string.Equals(c.Tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        #endregion

        [HttpGet("/Payment/My")]
        public IActionResult MyFromDashboard()
        {
            var selectedSlug = HttpContext.Session.GetString("SelectedConferenceSlug");
            if (!string.IsNullOrWhiteSpace(selectedSlug))
                return Redirect($"/{selectedSlug}/Payment/My");

            return Redirect("/Dashboard/MyConferences");
        }

        [HttpGet("My")]
        public async Task<IActionResult> My()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = GetSlug();
            var returnUrl = string.IsNullOrWhiteSpace(slug) ? "/Payment/My" : $"/{slug}/Payment/My";

            var selectedConferenceId = GetSelectedConferenceId();
            if (!selectedConferenceId.HasValue)
            {
                return RedirectToConferencePicker(
                    slug,
                    returnUrl,
                    _localizer["SelectConferenceForPaymentHistory"]);
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);

            if (conference == null)
            {
                HttpContext.Session.Remove("SelectedConferenceId");
                HttpContext.Session.Remove("SelectedConferenceSlug");

                return RedirectToConferencePicker(
                    slug,
                    returnUrl,
                    _localizer["SelectedConferenceNotFound"]);
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;
            if (!string.IsNullOrWhiteSpace(canonicalSlug) &&
                !string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect($"/{canonicalSlug}/Payment/My");
            }

            var payments = await _context.Payments
                .Include(p => p.Conference)
                .Where(p => p.AppUserId == user.Id && p.ConferenceId == conference.Id)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            return View(payments);
        }

        [HttpGet("/Payment/New")]
        [HttpGet("New")]
        public async Task<IActionResult> New()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = GetSlug();
            var selectedConferenceId = GetSelectedConferenceId();

            if (selectedConferenceId == null)
            {
                return RedirectToConferencePicker(
                    slug,
                    "/Payment/New",
                    _localizer["SelectConferenceBeforePayment"]);
            }

            var registration = await _context.Registrations
                .Include(r => r.Conference).ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(r => r.AppUserId == user.Id && r.ConferenceId == selectedConferenceId.Value);

            var targetSlug = !string.IsNullOrEmpty(slug)
                ? slug
                : registration?.Conference?.Tenant?.Slug ?? registration?.Conference?.Slug;

            if (registration == null)
            {
                TempData["InfoMessage"] = _localizer["RegisterBeforePayment"];

                if (string.IsNullOrEmpty(targetSlug))
                    return Redirect("/Dashboard");

                return Redirect($"/{targetSlug}/registration/join");
            }

            if (registration.IsPaid)
            {
                TempData["SuccessMessage"] = _localizer["PaymentAlreadyCompleted"];

                if (!string.IsNullOrEmpty(targetSlug))
                    return Redirect($"/{targetSlug}/Payment/My");

                return RedirectToAction(nameof(My));
            }

            if (!string.IsNullOrEmpty(targetSlug))
                return Redirect($"/{targetSlug}/Payment/Index/{registration.Id}");

            return RedirectToAction(nameof(Index), new { id = registration.Id });
        }

        [HttpGet("Index/{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference).ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == id && r.AppUserId == user.Id);

            if (registration == null)
                return NotFound(_localizer["RegistrationNotFound"]);

            var slug = GetSlug();
            if (!SlugMatches(registration.Conference, slug))
            {
                var canonicalSlug = registration.Conference?.Tenant?.Slug ?? registration.Conference?.Slug ?? slug;
                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                    return Redirect($"/{canonicalSlug}/Payment/Index/{id}");

                return NotFound(_localizer["RegistrationNotFound"]);
            }

            if (registration.IsPaid)
            {
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.RelatedSubmissionId == registration.Id);

                return RedirectToAction(nameof(Success), new { slug, id = existingPayment?.Id });
            }

            var paymentModel = new Payment
            {
                ConferenceId = registration.ConferenceId,
                Conference = registration.Conference,
                RelatedSubmissionId = registration.Id,
                Amount = registration.RegistrationType?.Price ?? 0,
                Currency = registration.RegistrationType?.Currency ?? "TL",
                BillingName = $"{user.FirstName} {user.LastName}"
            };

            return View(paymentModel);
        }

        [HttpPost("Process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Payment model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference).ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == model.RelatedSubmissionId && r.AppUserId == user.Id);

            if (registration == null)
                return NotFound(_localizer["RegistrationForPaymentNotFound"]);

            var slug = GetSlug();
            if (!SlugMatches(registration.Conference, slug))
            {
                var canonicalSlug = registration.Conference?.Tenant?.Slug ?? registration.Conference?.Slug ?? slug;
                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                    return Redirect($"/{canonicalSlug}/Payment/Index/{registration.Id}");

                return NotFound(_localizer["RegistrationForPaymentNotFound"]);
            }

            if (registration.IsPaid)
                return RedirectToAction(nameof(Success), new { slug });

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = registration.ConferenceId,
                RelatedSubmissionId = registration.Id,
                PaymentDate = DateTime.UtcNow,
                Status = PaymentStatus.Completed,

                Amount = registration.RegistrationType?.Price ?? 0,
                Currency = registration.RegistrationType?.Currency ?? "TL",

                BillingName = model.BillingName,
                BillingAddress = model.BillingAddress,
                TaxNumber = model.TaxNumber,
                TaxOffice = model.TaxOffice,
                PaymentMethod = "CreditCard",
                TransactionId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
            };

            _context.Payments.Add(payment);
            registration.IsPaid = true;

            await _context.SaveChangesAsync();

            await _notificationService.CreateAsync(
                userId: user.Id,
                title: _localizer["PaymentSuccessfulNotificationTitle"],
                message: string.Format(
                    _localizer["PaymentSuccessfulNotificationMessage"].Value,
                    payment.Amount,
                    payment.Currency),
                icon: "fas fa-check-circle",
                color: "success",
                link: "/Payment/My"
            );

            return RedirectToAction(nameof(Success), new { slug, id = payment.Id });
        }

        [HttpGet("Success")]
        public async Task<IActionResult> Success(Guid? id)
        {
            if (id == null) return View();

            var user = await _userManager.GetUserAsync(User);
            var payment = await _context.Payments
                .Include(p => p.Conference)
                .FirstOrDefaultAsync(p => p.Id == id && p.AppUserId == user.Id);

            if (payment == null)
                return RedirectToAction(nameof(My));

            return View(payment);
        }

        [HttpGet("Cancel")]
        public IActionResult Cancel()
        {
            return View();
        }
    }
}