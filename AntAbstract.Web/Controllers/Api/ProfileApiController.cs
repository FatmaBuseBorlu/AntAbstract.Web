using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AntAbstract.Web.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [EnableRateLimiting("api")]
    public class ProfileApiController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public ProfileApiController(
            UserManager<AppUser> userManager,
            AppDbContext context,
            INotificationService notificationService)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.FindByIdAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.University,
                user.Institution,
                user.City,
                user.OrcidId,
                user.ProfileImagePath,
                user.PhoneNumber,
                roles,
                emailConfirmed = user.EmailConfirmed,
                twoFactorEnabled = user.TwoFactorEnabled
            });
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications([FromQuery] int count = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, count);
            var unread = await _notificationService.GetUnreadCountAsync(userId);

            return Ok(new
            {
                unreadCount = unread,
                data = notifications.Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Icon,
                    n.Color,
                    n.Link,
                    n.IsRead,
                    n.CreatedAt
                })
            });
        }

        [HttpPost("notifications/{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var result = await _notificationService.MarkAsReadAsync(id, userId);
            return result ? Ok(new { success = true }) : NotFound();
        }

        [HttpGet("registrations")]
        public async Task<IActionResult> MyRegistrations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var registrations = await _context.Registrations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(r => r.Conference)
                .Include(r => r.RegistrationType)
                .Where(r => r.AppUserId == userId)
                .OrderByDescending(r => r.RegistrationDate)
                .Select(r => new
                {
                    r.Id,
                    r.IsPaid,
                    status = r.Status.ToString(),
                    r.Amount,
                    currency = r.RegistrationType != null ? r.RegistrationType.Currency : "TRY",
                    r.RegistrationDate,
                    r.PaymentDate,
                    registrationType = r.RegistrationType != null ? r.RegistrationType.Name : null,
                    conference = new { r.Conference.Id, r.Conference.Title },
                    hasReceipt = !string.IsNullOrWhiteSpace(r.ReceiptFilePath),
                    r.CheckedInAt
                })
                .ToListAsync();

            return Ok(new { count = registrations.Count, data = registrations });
        }

        [HttpGet("payments")]
        public async Task<IActionResult> MyPayments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var payments = await _context.Payments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(p => p.Conference)
                .Where(p => p.AppUserId == userId)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new
                {
                    p.Id,
                    p.Amount,
                    p.Currency,
                    status = p.Status.ToString(),
                    p.PaymentMethod,
                    p.PaymentDate,
                    p.TransactionId,
                    conference = new { p.Conference.Id, p.Conference.Title }
                })
                .ToListAsync();

            return Ok(new { count = payments.Count, data = payments });
        }
    }
}
