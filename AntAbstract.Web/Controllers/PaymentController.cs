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

        // GET: /Payment/Index/{id}
        // IMPORTANT: The parameter name must match the route parameter 'id'
        [HttpGet]
        public async Task<IActionResult> Index(Guid id)
        {
            if (id == Guid.Empty)
            {
                return NotFound("Payment ID is missing.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Find the registration record using the ID passed in the URL
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

            // Create the model for the view
            var paymentModel = new Payment
            {
                Amount = 1500, // Example fixed amount
                Currency = "TRY",
                BillingName = $"{user.FirstName} {user.LastName}",
                // Use the Registration ID for tracking
                RelatedSubmissionId = registration.Id,
                ConferenceId = registration.ConferenceId,
                Conference = registration.Conference
            };

            return View(paymentModel);
        }

        // ... (Keep your existing ProcessPayment method here) ...
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Payment model)
        {
            // ... (Your existing ProcessPayment logic) ...
            // Ensure this method is also present as you wrote it before
            return View(); // Placeholder return, use your actual logic
        }
    }
}