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
    // KRİTİK EKLEME: URL'in başında kongre adı (slug) olmalı
    [Route("{slug}/registration")]
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext; // Eklendi

        public RegistrationController(AppDbContext context,
                                      UserManager<AppUser> userManager,
                                      TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // 1. Kongre Kontrolü (URL'deki slug üzerinden)
            if (_tenantContext.Current == null)
            {
                return NotFound("Kongre bulunamadı (URL hatası).");
            }

            var user = await _userManager.GetUserAsync(User);

            // 2. Konferansı TenantID (Slug) üzerinden buluyoruz. 
            // ID'yi URL'den almaya gerek yok, Slug zaten tekil.
            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return NotFound("Bu URL'e bağlı bir kongre kaydı bulunamadı.");

            // 3. Zaten kayıtlı mı?
            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == conference.Id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                TempData["InfoMessage"] = "Bu kongreye zaten kaydınız bulunmaktadır.";
                // Yönlendirirken Slug'ı koruyoruz
                return RedirectToAction("Index", "Payment", new { slug = _tenantContext.Current.Slug, id = existingRegistration.Id });
            }

            return View(conference);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Create Action'ı için özel route: site.com/slug/registration/create
        [Route("create")]
        public async Task<IActionResult> Create(Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);

            // Slug kontrolü (Güvenlik için)
            if (_tenantContext.Current == null) return NotFound();

            bool alreadyRegistered = await _context.Registrations
                .AnyAsync(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id);

            if (alreadyRegistered)
            {
                // Slug ile yönlendirme
                return RedirectToAction("Index", "Payment", new { slug = _tenantContext.Current.Slug });
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

            // Ödeme sayfasına yönlendirirken Slug ve ID gönderiyoruz
            return RedirectToAction("Index", "Payment", new { slug = _tenantContext.Current.Slug, id = registration.Id });
        }
    }
}