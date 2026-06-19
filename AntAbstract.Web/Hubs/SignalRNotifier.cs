using AntAbstract.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AntAbstract.Web.Hubs
{
    public sealed class SignalRNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRNotifier(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public async Task SendAsync(string userId, object notification)
        {
            await _hub.Clients.Group($"user-{userId}")
                .SendAsync("ReceiveNotification", notification);
        }
    }
}
