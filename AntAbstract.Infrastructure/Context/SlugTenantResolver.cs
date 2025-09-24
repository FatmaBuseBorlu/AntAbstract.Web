using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Context
{
    public interface ITenantResolver
    {
        Task<Tenant?> ResolveAsync(HttpRequest request);
    }

    public class SlugTenantResolver : ITenantResolver
    {
        private readonly AppDbContext _db;
        public SlugTenantResolver(AppDbContext db) => _db = db;

        public async Task<Tenant?> ResolveAsync(HttpRequest request)
        {
            var first = request.Path.Value?
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(first)) return null;
            return await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == first);
        }
    }
}
