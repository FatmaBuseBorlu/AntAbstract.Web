using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize] 
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public RegistrationController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid id)
        {
            if (id == Guid.Empty) return NotFound();

            var user = await _userManager.GetUserAsync(User);


            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conference == null) return NotFound("Kongre bulunamadı.");

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                
                TempData["InfoMessage"] = "Bu kongreye zaten kaydınız bulunmaktadır.";
                return RedirectToAction("Index", "Payment", new { id = existingRegistration.Id });
            }

            return View(conference);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);

            bool alreadyRegistered = await _context.Registrations
                .AnyAsync(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id);

            if (alreadyRegistered)
            {
                return RedirectToAction("Index", "Payment");
            }

            var registration = new Registration
            {
                AppUserId = user.Id,
                ConferenceId = conferenceId,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,

            };

            _context.Registrations.Add(registration);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kaydınız oluşturuldu. Lütfen ödeme adımına geçiniz.";

            return RedirectToAction("Index", "Payment", new { id = registration.Id });
        }
    }
}