using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Domain.Entities
{
    public class Conference
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = null!;
        public string Code { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
    }
}
