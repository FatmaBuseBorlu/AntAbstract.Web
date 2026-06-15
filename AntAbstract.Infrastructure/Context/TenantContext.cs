using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Context
{
    public class TenantContext
    {
        public Tenant? Current { get; set; }

        public Guid? CurrentTenantId => Current?.Id;
    }
}
