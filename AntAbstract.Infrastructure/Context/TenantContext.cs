using AntAbstract.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Context
{
    public class TenantContext
    {
        public Tenant Current { get; set; }
    }
}
