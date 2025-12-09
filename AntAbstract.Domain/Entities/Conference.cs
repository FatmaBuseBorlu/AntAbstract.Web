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
        public string? City { get; set; }      
        public string? Country { get; set; } 
        public string? Venue { get; set; }     

        public string? LogoPath { get; set; }  
        public string? BannerPath { get; set; }
        public string? Slug { get; set; }      

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
    }
}
