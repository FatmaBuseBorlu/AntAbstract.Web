using System;

namespace AntAbstract.Infrastructure.Services.Conferences
{
    public interface ISelectedConferenceService
    {
        Guid? GetSelectedConferenceId();

        void SetSelectedConferenceId(Guid conferenceId);

        void ClearSelectedConferenceId();
    }
}