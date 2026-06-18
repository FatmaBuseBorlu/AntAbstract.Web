using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services
{

    public interface ITenantResolver
    {
        Task<Tenant?> ResolveAsync(HttpContext context);
    }

    public class SlugTenantResolver : ITenantResolver
    {
        private readonly AppDbContext _context;

        public SlugTenantResolver(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Tenant?> ResolveAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;

            if (string.IsNullOrEmpty(path) || path == "/")
            {
                return null;
            }

            var firstSegment = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (!string.IsNullOrEmpty(firstSegment))
            {
                return await _context.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug == firstSegment);
            }

            return null;
        }
    }
}
