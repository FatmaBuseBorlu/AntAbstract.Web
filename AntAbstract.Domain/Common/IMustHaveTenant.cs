using System;

namespace AntAbstract.Domain.Common 
{
    public interface IMustHaveTenant
    {
        public Guid TenantId { get; set; }
    }
}