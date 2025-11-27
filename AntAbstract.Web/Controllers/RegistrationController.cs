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
    [Authorize] // Kayıt olmak için giriş yapmış olmak şart
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public RegistrationController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Registration/Index/{conferenceId}
        [HttpGet]
        public async Task<IActionResult> Index(Guid id)
        {
            if (id == Guid.Empty) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            // 1. Kongreyi ve Bilet Tiplerini Çek
            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                // Eğer RegistrationTypes (Bilet Tipleri) tablonuzda veri varsa buraya Include eklenmeli
                // .Include(c => c.RegistrationTypes) 
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conference == null) return NotFound("Kongre bulunamadı.");

            // 2. Kullanıcı zaten kayıtlı mı kontrol et
            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.ConferenceId == id && r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                // Zaten kayıtlıysa direkt ödeme veya bilgi sayfasına yönlendir
                TempData["InfoMessage"] = "Bu kongreye zaten kaydınız bulunmaktadır.";
                // return RedirectToAction("Details", "Home", new { id = id }); 
                // VEYA Ödeme sayfasına:
                return RedirectToAction("Index", "Payment", new { id = existingRegistration.Id });
            }

            return View(conference);
        }

        // RegistrationController.cs içindeki Create (POST) metodu

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);

            // 1. Mükerrer kayıt kontrolü
            bool alreadyRegistered = await _context.Registrations
                .AnyAsync(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id);

            if (alreadyRegistered)
            {
                // Zaten kayıtlıysa o kaydı bul ve ödemeye git
                var existingReg = await _context.Registrations
                    .FirstOrDefaultAsync(r => r.ConferenceId == conferenceId && r.AppUserId == user.Id);
                return RedirectToAction("Index", "Payment", new { id = existingReg.Id });
            }

            // --- HATA ÇÖZÜMÜ: BİLET TİPİ ATAMA ---
            // Bu kongre için tanımlı bir bilet tipi (RegistrationType) var mı?
            var defaultRegType = await _context.RegistrationTypes
                .FirstOrDefaultAsync(rt => rt.ConferenceId == conferenceId);

            // Eğer yoksa, OTOMATİK bir tane oluştur (Hata almamak için)
            if (defaultRegType == null)
            {
                defaultRegType = new RegistrationType
                {
                    Id = Guid.NewGuid(),
                    Name = "Standart Katılım",
                    Description = "Genel kongre katılımı",
                    Price = 0, // Veya varsayılan bir fiyat
                    Currency = "TRY",
                    ConferenceId = conferenceId
                };
                _context.RegistrationTypes.Add(defaultRegType);
                await _context.SaveChangesAsync(); // Önce tipi kaydet
            }
            // -------------------------------------

            // 2. Yeni Kayıt Oluştur
            var registration = new Registration
            {
                AppUserId = user.Id,
                ConferenceId = conferenceId,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,

                // BURASI EKSİKTİ, ŞİMDİ DOLU GİDİYOR:
                RegistrationTypeId = defaultRegType.Id
            };

            _context.Registrations.Add(registration);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kaydınız başarıyla oluşturuldu. Ödeme adımına geçebilirsiniz.";

            // Kayıt olduktan sonra Ödeme Sayfasına yönlendir
            return RedirectToAction("Index", "Payment", new { id = registration.Id });
        }
    }
}