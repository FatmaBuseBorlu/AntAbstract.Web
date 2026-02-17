using AntAbstract.Domain.Entities;

namespace AntAbstract.Application.Interfaces
{
    public interface IConferencePageBlockService
    {
        Task<List<ConferencePageBlock>> GetBlocksAsync(Guid tenantId, Guid conferenceId, string page, string culture);
    }
}
