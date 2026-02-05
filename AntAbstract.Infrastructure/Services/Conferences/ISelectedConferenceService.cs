using System.Security.Cryptography;

namespace AntAbstract.Infrastructure.Services.Conferences
{
    public interface ISelectedConferenceService
    {
        Guid? GetSelectedConferenceId();
        void SetSelectedConferenceId(Guid conferenceId);
        void ClearSelectedConferenceId();
    }
}
