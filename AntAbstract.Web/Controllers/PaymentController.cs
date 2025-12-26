using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    [Route("{slug}/payment")]
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;

        public PaymentController(AppDbContext context, UserManager<AppUser> userManager, TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

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

            if (!string.IsNullOrWhiteSpace(c.Slug) && string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return true;

            if (c.Tenant != null && !string.IsNullOrWhiteSpace(c.Tenant.Slug) && string.Equals(c.Tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        [HttpGet("/payment/my")]
        public IActionResult MyFromDashboard()
        {
            var selectedSlug = HttpContext.Session.GetString("SelectedConferenceSlug");
            if (!string.IsNullOrWhiteSpace(selectedSlug))
                return Redirect($"/{selectedSlug}/payment/my");

            return Redirect("/Dashboard/MyConferences");
        }

        [HttpGet("my")]
        public async Task<IActionResult> My()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var slug = GetSlug();
            var returnUrl = string.IsNullOrWhiteSpace(slug) ? "/payment/my" : $"/{slug}/payment/my";

            var selectedConferenceId = GetSelectedConferenceId();
            if (!selectedConferenceId.HasValue)
                return RedirectToConferencePicker(slug, returnUrl, "Ödeme için önce kongre seçmelisiniz.");

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);

            if (conference == null)
            {
                HttpContext.Session.Remove("SelectedConferenceId");
                HttpContext.Session.Remove("SelectedConferenceSlug");
                return RedirectToConferencePicker(slug, returnUrl, "Seçili kongre bulunamadı. Lütfen yeniden seçin.");
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;
            if (!string.IsNullOrWhiteSpace(canonicalSlug) && !string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
                return Redirect($"/{canonicalSlug}/payment/my");

            var reg = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (reg == null)
                return RedirectToConferencePicker(canonicalSlug, returnUrl, "Ödeme için önce kongre kaydı gerekli. Lütfen önce kayıt olun.");

            if (reg.IsPaid)
                return RedirectToAction(nameof(Success), new { slug = canonicalSlug });

            return RedirectToAction(nameof(Index), new { slug = canonicalSlug, id = reg.Id });
        }

        [HttpGet("index/{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference).ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == id && r.AppUserId == user.Id);

            if (registration == null)
                return NotFound("Kayıt bulunamadı.");

            var slug = GetSlug();
            if (!SlugMatches(registration.Conference, slug))
            {
                var canonicalSlug = registration.Conference?.Tenant?.Slug ?? registration.Conference?.Slug ?? slug;
                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                    return Redirect($"/{canonicalSlug}/payment/index/{id}");

                return NotFound("Kayıt bulunamadı.");
            }

            if (registration.IsPaid)
                return RedirectToAction(nameof(Success), new { slug });

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

        [HttpGet("success")]
        public IActionResult Success()
        {
            return View();
        }

        [HttpGet("cancel")]
        public IActionResult Cancel()
        {
            return View();
        }

        [HttpPost("process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Payment model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference).ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == model.RelatedSubmissionId && r.AppUserId == user.Id);

            if (registration == null)
                return NotFound("Ödeme yapılacak kayıt bulunamadı.");

            var slug = GetSlug();
            if (!SlugMatches(registration.Conference, slug))
            {
                var canonicalSlug = registration.Conference?.Tenant?.Slug ?? registration.Conference?.Slug ?? slug;
                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                    return Redirect($"/{canonicalSlug}/payment/index/{registration.Id}");

                return NotFound("Ödeme yapılacak kayıt bulunamadı.");
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
                BillingName = $"{user.FirstName} {user.LastName}",
                Conference = null
            };

            _context.Payments.Add(payment);
            registration.IsPaid = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Success), new { slug });
        }
    }
}
