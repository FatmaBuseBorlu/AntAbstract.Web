using AntAbstract.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public class Conference
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? City { get; set; }      // Şehir
        public string? Country { get; set; }   // Ülke
        public string? Venue { get; set; }     // Mekan Adı (Otel/Kongre Merkezi)

        public string? LogoPath { get; set; }  // Logo resim yolu
        public string? BannerPath { get; set; } // Arkaplan resim yolu
        public string? Slug { get; set; }      // URL uzantısı (örn: vet2025)

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
    }
}
