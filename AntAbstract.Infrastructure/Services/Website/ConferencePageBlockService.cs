using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Infrastructure.Services
{
    public class ConferencePageBlockService : IConferencePageBlockService
    {
        private readonly AppDbContext _context;

        public ConferencePageBlockService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ConferencePageBlock>> GetBlocksAsync(Guid tenantId, Guid conferenceId, string page, string culture)
        {
            var blocks = await _context.ConferencePageBlocks
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ConferenceId == conferenceId &&
                    x.Page == page &&
                    x.Culture == culture &&
                    x.IsActive)
                .OrderBy(x => x.Order)
                .ToListAsync();

            if (blocks.Count > 0) return blocks;

            blocks = await _context.ConferencePageBlocks
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ConferenceId == conferenceId &&
                    x.Page == page &&
                    x.Culture == "tr-TR" &&
                    x.IsActive)
                .OrderBy(x => x.Order)
                .ToListAsync();

            if (blocks.Count > 0) return blocks;

            return await _context.ConferencePageBlocks
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ConferenceId == conferenceId &&
                    x.Page == page &&
                    x.IsActive)
                .OrderBy(x => x.Order)
                .ToListAsync();
        }
    }
}
