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
        public async Task<IActionResult> Index(string backUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = RouteData.Values["slug"]?.ToString();

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction("Index", "Home");
            }

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                TempData["InfoMessage"] = "Zaten kaydınız mevcut. Ödeme ekranına yönlendirildiniz.";
                return RedirectToAction("Index", "Payment", new { slug = slug, id = existingRegistration.Id });
            }

            var defaultRegType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(rt => rt.ConferenceId == conference.Id);

            ViewBag.Price = defaultRegType?.Price ?? 0;
            ViewBag.Currency = defaultRegType?.Currency ?? "TL";
            ViewBag.BackUrl = backUrl;

            return View(conference);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = RouteData.Values["slug"]?.ToString();

            var conference = await _context.Conferences.FindAsync(conferenceId);
            if (conference == null) return NotFound();

            var exists = await _context.Registrations.AnyAsync(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id);
            if (exists)
            {
                var existingId = await _context.Registrations
                    .Where(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                return RedirectToAction("Index", "Payment", new { slug = slug, id = existingId });
            }

            var defaultRegType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(rt => rt.ConferenceId == conferenceId);

            if (defaultRegType == null)
            {
                defaultRegType = new RegistrationType
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    Name = "Standart Katılım",
                    Description = "Otomatik oluşturulan kayıt tipi",
                    Price = 0,
                    Currency = "TL"
                };
                _context.RegistrationTypes.Add(defaultRegType);
                await _context.SaveChangesAsync();
            }

            var newRegistration = new Registration
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = conferenceId,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,

                RegistrationTypeId = defaultRegType.Id
            };

            _context.Registrations.Add(newRegistration);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kaydınız başarıyla oluşturuldu. Lütfen ödemenizi tamamlayınız.";

            return RedirectToAction("Index", "Payment", new { slug = slug, id = newRegistration.Id });
        }
    }
}