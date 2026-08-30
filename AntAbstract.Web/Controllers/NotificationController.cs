using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        // Kurucu metoda dokunmadan çeviri: mesajlar eskiden doğrudan Türkçe
        // yazılıydı, İngilizce seçili kullanıcıya da Türkçe dönüyorlardı.
        private string T(string key, string fallback)
        {
            var value = HttpContext?.RequestServices
                .GetService<IStringLocalizer<NotificationController>>()?[key];

            return value == null || value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context;

        public NotificationController(
            INotificationService notificationService,
            AppDbContext context)
        {
            _notificationService = notificationService;
            _context = context;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return View("~/Views/Notification/Index.cshtml", notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = T("Msg_GecersizBildirim", "Geçersiz bildirim.")
                });
            }

            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    ok = false,
                    message = "Kullanıcı oturumu bulunamadı."
                });
            }

            var result = await _notificationService.MarkAsReadAsync(id, userId);

            if (!result)
            {
                return NotFound(new
                {
                    ok = false,
                    message = T("Msg_BildirimBulunamadiVeyaBuBildirimeErisim", "Bildirim bulunamadı veya bu bildirime erişim yetkiniz yok.")
                });
            }

            return Ok(new
            {
                ok = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReadPage(int id)
        {
            if (id <= 0)
            {
                TempData["ErrorMessage"] = T("Msg_GecersizBildirim", "Geçersiz bildirim.");
                return Redirect("/Notification/Index");
            }

            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var result = await _notificationService.MarkAsReadAsync(id, userId);

            if (!result)
            {
                TempData["ErrorMessage"] = T("Msg_BildirimBulunamadiVeyaBuBildirimeErisim", "Bildirim bulunamadı veya bu bildirime erişim yetkiniz yok.");
            }

            return Redirect("/Notification/Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    ok = false,
                    message = "Kullanıcı oturumu bulunamadı."
                });
            }

            await _notificationService.MarkAllAsReadAsync(userId);

            return Ok(new
            {
                ok = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllReadPage()
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            await _notificationService.MarkAllAsReadAsync(userId);

            return Redirect("/Notification/Index");
        }
    }
}