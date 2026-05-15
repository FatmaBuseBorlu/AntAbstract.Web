using AntAbstract.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
                    message = "Geçersiz bildirim."
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
                    message = "Bildirim bulunamadı veya bu bildirime erişim yetkiniz yok."
                });
            }

            return Ok(new
            {
                ok = true
            });
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
    }

}
