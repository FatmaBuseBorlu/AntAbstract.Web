using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        // 1) Konferanstan ödeme sayfasına geçiş
        // URL: /{slug}/payment/payforconference?conferenceId={GUID}
        [HttpGet("payforconference", Name = "PayForConferenceRoute")]
        public async Task<IActionResult> PayForConference(Guid conferenceId)
        {
            var slug = RouteData.Values["slug"]?.ToString();
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound("Slug bulunamadı.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var registration = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ConferenceId == conferenceId && r.AppUserId == userId);

            if (registration == null)
                return NotFound("Bu konferans için kayıt bulunamadı. Önce kayıt oluşturmalısın.");

            return RedirectToAction("Index", new { slug = slug, id = registration.Id });
        }

        // 2) Ödeme sayfası
        // URL: /{slug}/payment/index/{GUID}
        [HttpGet("index/{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
                return NotFound("Ödeme kaydı bulunamadı.");

            return View(registration);
        }

        // 3) Ödemeyi tamamla
        // POST: /{slug}/payment/process
        [HttpPost("process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Guid registrationId)
        {
            var registration = await _context.Registrations
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration == null)
                return NotFound();

            registration.IsPaid = true;
            registration.PaymentDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödemeniz başarıyla alındı!";
            return RedirectToAction("Index", "Home", new { slug = RouteData.Values["slug"] });
        }
    }
}
