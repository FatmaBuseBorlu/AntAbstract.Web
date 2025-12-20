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
        public async Task<IActionResult> Join(string backUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var slug = RouteData.Values["slug"]?.ToString();
            if (string.IsNullOrWhiteSpace(slug))
            {
                TempData["ErrorMessage"] = "Slug bulunamadı.";
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                if (!string.IsNullOrWhiteSpace(backUrl) && Url.IsLocalUrl(backUrl)) return Redirect(backUrl);
                return RedirectToAction("Index", "Home");
            }

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                TempData["SuccessMessage"] = "Zaten kayıtlısınız. Ödeme yapabilirsiniz.";
                if (!string.IsNullOrWhiteSpace(backUrl) && Url.IsLocalUrl(backUrl)) return Redirect(backUrl);
                return RedirectToAction("Index", "Home");
            }

            var defaultRegType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(rt => rt.ConferenceId == conference.Id);

            if (defaultRegType == null)
            {
                defaultRegType = new RegistrationType
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conference.Id,
                    Name = "Standart Kayıt",
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
                ConferenceId = conference.Id,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,
                RegistrationTypeId = defaultRegType.Id
            };

            _context.Registrations.Add(newRegistration);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kaydınız başarıyla yapıldı. Artık ödeme yapabilirsiniz.";

            if (!string.IsNullOrWhiteSpace(backUrl) && Url.IsLocalUrl(backUrl)) return Redirect(backUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}
