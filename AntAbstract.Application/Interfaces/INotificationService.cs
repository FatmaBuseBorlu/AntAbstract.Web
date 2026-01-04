using AntAbstract.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces 
{
    public interface INotificationService
    {
        Task CreateAsync(string userId, string title, string message, string icon, string color, string link = null);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 10);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(Guid notificationId);
        Task MarkAllAsReadAsync(string userId);
    }
}