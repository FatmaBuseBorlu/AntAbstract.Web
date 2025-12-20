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

        [HttpGet("index/{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);

            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null) return NotFound("Kayıt bulunamadı.");

            if (registration.IsPaid)
            {
                return RedirectToAction("Success", new { slug = RouteData.Values["slug"] });
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
            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(r => r.Id == model.RelatedSubmissionId);

            if (registration == null) return NotFound("Ödeme yapılacak kayıt bulunamadı.");

            var user = await _userManager.GetUserAsync(User);

            model.Id = Guid.NewGuid();
            model.PaymentDate = DateTime.UtcNow;
            model.AppUserId = user.Id;

            model.Status = PaymentStatus.Completed;

            model.Conference = null;

            _context.Payments.Add(model);

            registration.IsPaid = true;

            await _context.SaveChangesAsync();

            var slug = RouteData.Values["slug"];
            return RedirectToAction("Success", new { slug = slug });
        }

        [HttpGet("success")]
        public IActionResult Success()
        {
            return View();
        }
    }
}