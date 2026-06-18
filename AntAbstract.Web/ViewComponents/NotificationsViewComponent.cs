using AntAbstract.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.ViewComponents
{
    public class NotificationViewComponent : ViewComponent
    {
        private readonly INotificationService _notificationService;

        public NotificationViewComponent(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Content("");

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, 8);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            ViewBag.UnreadCount = unreadCount;
            return View(notifications);
        }
    }
}