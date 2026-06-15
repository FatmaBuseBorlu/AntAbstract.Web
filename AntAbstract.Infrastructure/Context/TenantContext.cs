using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Context
{
    public class TenantContext
    {
        public Tenant? Current { get; set; }

        /// <summary>
        /// SuperAdmin veya seeding gibi kasıtlı global erişim bağlamlarında
        /// true yapılır. Bu bayrak set edilmeden CurrentTenantId null ise
        /// tenant filtresi hiçbir satırı döndürmez (güvenli varsayılan).
        /// </summary>
        public bool IsGlobalContext { get; set; } = false;

        public Guid? CurrentTenantId => Current?.Id;
    }
}
