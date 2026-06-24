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
        Task<Conference?> ResolveConferenceAsync(HttpContext context);
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
            var slug = GetSlugFromPath(context);
            if (string.IsNullOrEmpty(slug)) return null;

            // First try: resolve via Conference.Slug → get its Tenant
            var conference = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Slug == slug);

            if (conference?.Tenant != null)
            {
                context.Items["ResolvedConference"] = conference;
                return conference.Tenant;
            }

            // Fallback: resolve via Tenant.Slug (backward compatibility)
            return await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug);
        }

        public async Task<Conference?> ResolveConferenceAsync(HttpContext context)
        {
            if (context.Items.TryGetValue("ResolvedConference", out var cached) && cached is Conference conf)
                return conf;

            var slug = GetSlugFromPath(context);
            if (string.IsNullOrEmpty(slug)) return null;

            var conference = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Slug == slug);

            if (conference != null)
                context.Items["ResolvedConference"] = conference;

            return conference;
        }

        private static string? GetSlugFromPath(HttpContext context)
        {
            var path = context.Request.Path.Value;
            if (string.IsNullOrEmpty(path) || path == "/") return null;

            return path.Split('/', System.StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
    }
}
