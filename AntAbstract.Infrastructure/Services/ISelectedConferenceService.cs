namespace AntAbstract.Infrastructure.Services
{
    public interface ISelectedConferenceService
    {
        Guid? GetSelectedConferenceId();
        void SetSelectedConferenceId(Guid conferenceId);
        void Clear();
    }
}
