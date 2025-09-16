using AntAbstract.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Context
{
    public sealed class TenantContext
    {
        public Tenant? Current { get; set; }
    }
}
