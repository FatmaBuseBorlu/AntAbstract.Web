namespace AntAbstract.Application.Interfaces
{
    public interface IRealtimeNotifier
    {
        Task SendAsync(string userId, object notification);
    }
}
