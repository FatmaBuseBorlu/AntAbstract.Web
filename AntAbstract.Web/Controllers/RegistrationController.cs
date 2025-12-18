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
    [Route("{slug}/registration")]
    [Route("registration")]
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;

        public RegistrationController(AppDbContext context,
                                      UserManager<AppUser> userManager,
                                      TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        [HttpGet("")]
        [HttpGet("index/{id?}")]
        public async Task<IActionResult> Index(Guid? id)
        {
            var user = await _userManager.GetUserAsync(User);
            Conference conference = null;

            if (_tenantContext.Current != null)
            {
                conference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
            }
            else if (id.HasValue)
            {
                conference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Id == id.Value);
            }

            if (conference == null) return NotFound("Kongre bulunamadı.");

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                TempData["InfoMessage"] = "Bu kongreye zaten kaydınız bulunmaktadır.";
                return RedirectToAction("Index", "Payment", new { slug = conference.Tenant?.Slug, id = existingRegistration.Id });
            }

            return View(conference);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);
            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null) return NotFound();

            bool alreadyRegistered = await _context.Registrations
                .AnyAsync(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id);

            if (alreadyRegistered)
            {
                return RedirectToAction("Index", "Payment", new { slug = conference.Tenant?.Slug });
            }

            var defaultRegType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(rt => rt.ConferenceId == conferenceId)
                ?? await _context.RegistrationTypes.FirstOrDefaultAsync();

            if (defaultRegType == null)
            {
                TempData["ErrorMessage"] = "Sistemde tanımlı kayıt tipi bulunamadı.";
                return RedirectToAction("Index");
            }

            var registration = new Registration
            {
                AppUserId = user.Id,
                ConferenceId = conferenceId,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,
                RegistrationTypeId = defaultRegType.Id
            };

            _context.Registrations.Add(registration);
            await _context.SaveChangesAsync();

            // YÖNLENDİRME DEĞİŞTİ: Önce başarı sayfasına gidiyoruz
            return RedirectToAction("Success", new { slug = conference.Tenant?.Slug, id = registration.Id });
        }

        // YENİ EKLEME: Başarı Sayfası
        [HttpGet("success/{id}")]
        public IActionResult Success(Guid id)
        {
            ViewBag.RegistrationId = id;
            return View();
        }
    }
}