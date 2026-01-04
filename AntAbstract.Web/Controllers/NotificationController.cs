using AntAbstract.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
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

        [HttpPost]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                await _notificationService.MarkAllAsReadAsync(userId);
            }
            return Ok();
        }
    }
}