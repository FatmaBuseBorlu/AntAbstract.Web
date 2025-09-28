using AntAbstract.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Slug { get; set; } = null!;  // ör: icc2025
        public string Name { get; set; } = null!;  // Kongre adı
        public string? ThemeJson { get; set; }     // Tema bilgileri (opsiyonel)                                      
        public string? LogoUrl { get; set; } // Logo resminin URL'sini tutacak
        public int? ScientificFieldId { get; set; }
        [ForeignKey("ScientificFieldId")]
        public ScientificField? ScientificField { get; set; }

        public int? CongressTypeId { get; set; }
        [ForeignKey("CongressTypeId")]
        public CongressType? CongressType { get; set; }
    }
}
