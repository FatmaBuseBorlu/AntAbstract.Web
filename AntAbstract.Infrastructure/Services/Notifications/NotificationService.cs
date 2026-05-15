using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Notifications
{
    public class NotificationService : INotificationService
    {
        private const int MaxTitleLength = 200;
        private const int MaxMessageLength = 1000;
        private const int MaxIconLength = 100;
        private const int MaxColorLength = 50;
        private const int MaxLinkLength = 500;

        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(
            string userId,
            string title,
            string message,
            string icon,
            string color,
            string? link = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            title = Normalize(title, MaxTitleLength);
            message = Normalize(message, MaxMessageLength);
            icon = Normalize(icon, MaxIconLength);
            color = Normalize(color, MaxColorLength);
            link = NormalizeNullable(link, MaxLinkLength);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Icon = icon,
                Color = color,
                Link = link,
                IsRead = false,
                CreatedDate = DateTime.UtcNow
            };

            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 10)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<Notification>();
            }

            if (count <= 0)
            {
                count = 10;
            }

            if (count > 100)
            {
                count = 100;
            }

            return await _context.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            return await _context.Notifications
                .AsNoTracking()
                .CountAsync(x =>
                    x.UserId == userId &&
                    !x.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
        {
            if (notificationId <= 0 || string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(x =>
                    x.Id == notificationId &&
                    x.UserId == userId);

            if (notification == null)
            {
                return false;
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var unreadList = await _context.Notifications
                .Where(x =>
                    x.UserId == userId &&
                    !x.IsRead)
                .ToListAsync();

            if (!unreadList.Any())
            {
                return;
            }

            foreach (var item in unreadList)
            {
                item.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        private static string Normalize(string? value, int maxLength)
        {
            value = value?.Trim() ?? string.Empty;

            if (value.Length > maxLength)
            {
                value = value.Substring(0, maxLength);
            }

            return value;
        }

        private static string? NormalizeNullable(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            if (value.Length > maxLength)
            {
                value = value.Substring(0, maxLength);
            }

            return value;
        }
    }
}