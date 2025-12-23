using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
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

        public PaymentController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("/payment/my")]
        public async Task<IActionResult> MyFromDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var reg = await _context.Registrations
                .Include(r => r.Conference)
                    .ThenInclude(c => c.Tenant)
                .OrderByDescending(r => r.RegistrationDate)
                .FirstOrDefaultAsync(r => r.AppUserId == user.Id && !r.IsPaid);

            if (reg == null)
            {
                TempData["ErrorMessage"] = "Ödeme bulunamadı. Önce bir kongreye kayıt olun.";
                return Redirect("/Dashboard");
            }

            var conferenceSlug =
                !string.IsNullOrEmpty(reg.Conference?.Tenant?.Slug) ? reg.Conference.Tenant.Slug :
                reg.Conference?.Slug;

            if (string.IsNullOrWhiteSpace(conferenceSlug))
            {
                TempData["ErrorMessage"] = "Kongre slug bulunamadı.";
                return Redirect("/Dashboard");
            }

            return Redirect($"/{conferenceSlug}/payment/index/{reg.Id}");
        }

        [HttpGet("my")]
        public async Task<IActionResult> My()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = RouteData.Values["slug"]?.ToString();
            if (string.IsNullOrWhiteSpace(slug))
            {
                TempData["ErrorMessage"] = "Slug bulunamadı.";
                return Redirect("/Dashboard");
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return Redirect("/Dashboard");
            }

            var reg = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (reg == null)
            {
                TempData["ErrorMessage"] = "Ödeme için önce kongre kaydı gerekli. Lütfen önce kayıt olun.";
                return Redirect("/Dashboard");
            }

            if (reg.IsPaid)
            {
                return RedirectToAction("Success", new { slug = slug });
            }

            return RedirectToAction("Index", new { slug = slug, id = reg.Id });
        }

        [HttpGet("index/{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == id && r.AppUserId == user.Id);

            if (registration == null) return NotFound("Kayıt bulunamadı.");

            var slug = RouteData.Values["slug"]?.ToString();
            if (registration.Conference == null || !string.Equals(registration.Conference.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound("Kayıt bulunamadı.");
            }

            if (registration.IsPaid)
            {
                return RedirectToAction("Success", new { slug = slug });
            }

            var paymentModel = new Payment
            {
                ConferenceId = registration.ConferenceId,
                Conference = registration.Conference,
                RelatedSubmissionId = registration.Id,
                Amount = registration.RegistrationType != null ? registration.RegistrationType.Price : 0,
                Currency = registration.RegistrationType != null ? registration.RegistrationType.Currency : "TL",
                BillingName = $"{user.FirstName} {user.LastName}"
            };

            return View(paymentModel);
        }

        [HttpPost("process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Payment model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == model.RelatedSubmissionId && r.AppUserId == user.Id);

            if (registration == null) return NotFound("Ödeme yapılacak kayıt bulunamadı.");

            var slug = RouteData.Values["slug"]?.ToString();
            if (registration.Conference == null || !string.Equals(registration.Conference.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound("Ödeme yapılacak kayıt bulunamadı.");
            }

            if (registration.IsPaid)
            {
                return RedirectToAction("Success", new { slug = slug });
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = registration.ConferenceId,
                RelatedSubmissionId = registration.Id,
                PaymentDate = DateTime.UtcNow,
                Status = PaymentStatus.Completed,
                Amount = registration.RegistrationType != null ? registration.RegistrationType.Price : 0,
                Currency = registration.RegistrationType != null ? registration.RegistrationType.Currency : "TL",
                BillingName = $"{user.FirstName} {user.LastName}",
                Conference = null
            };

            _context.Payments.Add(payment);
            registration.IsPaid = true;

            await _context.SaveChangesAsync();

            return RedirectToAction("Success", new { slug = slug });
        }

        [HttpGet("success")]
        public IActionResult Success()
        {
            return View();
        }
    }
}
