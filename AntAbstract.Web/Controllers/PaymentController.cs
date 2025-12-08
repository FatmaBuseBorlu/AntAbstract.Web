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
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PaymentController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        [HttpGet]
        public async Task<IActionResult> PayForConference(Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge(); 

           
            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id);

            if (registration == null)
            {
                TempData["InfoMessage"] = "Ödeme yapabilmek için önce kayıt olmalısınız.";
                return RedirectToAction("Index", "Registration", new { id = conferenceId });
            }

            return RedirectToAction("Index", new { id = registration.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid id)
        {
            if (id == Guid.Empty)
            {
                return NotFound("Payment ID is missing.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(r => r.Id == id && r.AppUserId == user.Id);

            if (registration == null)
            {
                return NotFound($"Registration not found for ID: {id}");
            }

            if (registration.IsPaid)
            {
                TempData["InfoMessage"] = "This registration is already paid.";
                return RedirectToAction("Index", "Dashboard");
            }

            var paymentModel = new Payment
            {
                Amount = 1500, 
                Currency = "TRY",
                BillingName = $"{user.FirstName} {user.LastName}",
                RelatedSubmissionId = registration.Id,
                ConferenceId = registration.ConferenceId,
                Conference = registration.Conference
            };

            return View(paymentModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Payment model)
        {
            return View(); 
        }
    }
}