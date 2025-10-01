using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // Bu sayfaya sadece giriş yapmış kullanıcılar erişebilsin
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;

        public RegistrationController(AppDbContext context, TenantContext tenantContext, UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
        }

        // GET: /Registration/Index
        // Mevcut kayıt türlerini listeleyen ve kullanıcıya seçim sunan sayfa
        public async Task<IActionResult> Index()
        {
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null) return Content("Bu kongre için kayıt seçenekleri henüz aktif değil.");

            // Kullanıcının bu kongreye zaten bir kaydı var mı diye kontrol et
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingRegistration = await _context.Registrations
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.AppUserId == userId && r.ConferenceId == conference.Id);

            // Eğer zaten bir kaydı varsa, onu detay sayfasına yönlendir
            if (existingRegistration != null)
            {
                return RedirectToAction(nameof(Details), new { id = existingRegistration.Id });
            }

            // Eğer kaydı yoksa, mevcut kayıt türlerini listele
            var registrationTypes = await _context.RegistrationTypes
                .Where(rt => rt.ConferenceId == conference.Id)
                .ToListAsync();

            return View(registrationTypes);
        }
        // Controllers/RegistrationController.cs içine eklenecek

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Guid registrationTypeId)
        {
            // 1. Gerekli bilgileri al: aktif kullanıcı, aktif kongre
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null || string.IsNullOrEmpty(userId))
            {
                // Gerekli bilgiler yoksa hata yönetimi
                return RedirectToAction("Index", "Home");
            }

            // 2. Kullanıcının bu kongreye zaten bir kaydı var mı diye tekrar kontrol et
            var alreadyRegistered = await _context.Registrations
                .AnyAsync(r => r.AppUserId == userId && r.ConferenceId == conference.Id);

            if (alreadyRegistered)
            {
                // Zaten kayıtlıysa bir uyarı göster
                TempData["ErrorMessage"] = "Bu kongreye zaten kayıt olmuşsunuz.";
                return RedirectToAction(nameof(Index));
            }

            // 3. Yeni bir kayıt (Registration) nesnesi oluştur
            var newRegistration = new Registration
            {
                AppUserId = userId,
                ConferenceId = conference.Id,
                RegistrationTypeId = registrationTypeId,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false // Ödeme henüz yapılmadı
            };

            // 4. Yeni kaydı veritabanına ekle
            _context.Registrations.Add(newRegistration);
            await _context.SaveChangesAsync();

            // 5. Kullanıcıyı, kaydının detaylarını göreceği bir sonraki sayfaya yönlendir
            //    Bu sayfa aynı zamanda "Şimdi Öde" butonunu içerecek
            return RedirectToAction(nameof(Details), new { id = newRegistration.Id });
        }

        // Controllers/RegistrationController.cs içine eklenecek

        // GET: /Registration/Details/5
        public async Task<IActionResult> Details(Guid id)
        {
            var registration = await _context.Registrations
                .Include(r => r.AppUser)
                .Include(r => r.Conference)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
            {
                return NotFound();
            }

            // Güvenlik kontrolü: Sadece kendi kaydını görebilmesini sağla
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (registration.AppUserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(registration);
        }


    }
}