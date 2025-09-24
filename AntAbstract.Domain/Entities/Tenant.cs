using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Domain.Entities
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Slug { get; set; } = null!;  // ör: icc2025
        public string Name { get; set; } = null!;  // Kongre adı
        public string? ThemeJson { get; set; }     // Tema bilgileri (opsiyonel)
    }
}
