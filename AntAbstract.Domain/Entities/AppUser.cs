using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;




namespace AntAbstract.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        [StringLength(500)]
        public string? ExpertiseAreas { get; set; } // Örn: "yapay zeka, siber güvenlik, veritabanı"
    }
}
