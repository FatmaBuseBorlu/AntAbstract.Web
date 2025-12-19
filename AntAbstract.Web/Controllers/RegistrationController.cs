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
    [Route("{slug}/registration")]
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;

        public RegistrationController(AppDbContext context, UserManager<AppUser> userManager, TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        [HttpGet("join")]
        public async Task<IActionResult> Join()
        {
            var user = await _userManager.GetUserAsync(User);
            var slug = RouteData.Values["slug"]?.ToString();

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Tenant.Slug == slug);

            if (conference == null) return NotFound("Kongre bulunamadı.");

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                return RedirectToAction("Index", "Home", new { slug = slug });
            }

            var defaultRegType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(rt => rt.ConferenceId == conference.Id)
                ?? await _context.RegistrationTypes.FirstOrDefaultAsync();

            if (defaultRegType == null) return RedirectToAction("Index", "Home");

            var newRegistration = new Registration
            {
                AppUserId = user.Id,
                ConferenceId = conference.Id,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,
                RegistrationTypeId = defaultRegType.Id
            };

            _context.Registrations.Add(newRegistration);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kaydınız başarıyla yapıldı! Şimdi ödeme yapabilirsiniz.";

            return RedirectToAction("Index", "Home", new { slug = slug });
        }
    }
}