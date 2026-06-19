using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AntAbstract.Web.Hubs
{
    [Authorize]
    public sealed class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(userId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");

            await base.OnDisconnectedAsync(exception);
        }
    }
}
