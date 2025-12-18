using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    [Route("{slug}/payment")]
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;

        public PaymentController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("index/{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            // Kayıt bilgilerini ve fiyatı çekiyoruz
            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null) return NotFound("Ödeme kaydı bulunamadı.");

            return View(registration);
        }

        [HttpPost("process")]
        public async Task<IActionResult> ProcessPayment(Guid registrationId, string slug)
        {
            var registration = await _context.Registrations.FindAsync(registrationId);
            if (registration == null) return NotFound();

            // Ödeme başarılı simülasyonu
            registration.IsPaid = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödemeniz başarıyla alındı. Kaydınız kesinleşmiştir.";

            // Kullanıcıyı kongre ana sayfasına geri gönder
            return RedirectToAction("Index", "Home", new { slug = slug });
        }
    }
}