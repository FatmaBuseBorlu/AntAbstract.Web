using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services
{
    // Önce Interface'i (Şablonu) tanımlayalım
    public interface ITenantResolver
    {
        Task<Tenant> ResolveAsync(HttpContext context);
    }

    // Dedektif Sınıfı
    public class SlugTenantResolver : ITenantResolver
    {
        private readonly AppDbContext _context;

        public SlugTenantResolver(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Tenant> ResolveAsync(HttpContext context)
        {
            // 1. URL'nin yolunu al (Örn: /vet2025/Home/Index)
            var path = context.Request.Path.Value;

            if (string.IsNullOrEmpty(path) || path == "/")
            {
                return null; // Ana sayfadayız, tenant yok
            }

            // 2. İlk parçayı (slug) ayıkla (Örn: vet2025)
            var firstSegment = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (!string.IsNullOrEmpty(firstSegment))
            {
                // 3. Veritabanında bu slug'a sahip bir kongre (Tenant) var mı?
                var tenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.Slug == firstSegment);

                return tenant; // Varsa döndür, yoksa null döner
            }

            return null;
        }
    }
}