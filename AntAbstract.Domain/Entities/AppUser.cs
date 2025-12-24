using Microsoft.AspNetCore.Http;
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
        [StringLength(50)]
        public string? FirstName { get; set; }

        [StringLength(50)]
        public string? LastName { get; set; }

        [StringLength(20)]
        public string? IdentityNumber { get; set; } 
        public string? AlternativeEmail { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }

        public string? University { get; set; } 

        [StringLength(200)]
        public string? Institution { get; set; } 

        [StringLength(100)]
        public string? Title { get; set; } 

        public string? Profession { get; set; } 

        [StringLength(500)]
        public string? ExpertiseAreas { get; set; } 
        public string? DisplayName { get; set; } 
        public string? ProfileImagePath { get; set; }

        [StringLength(50)]
        public string? OrcidId { get; set; }
        [StringLength(100)]
        public string? ResearcherId { get; set; }
        [StringLength(200)]
        public string? GoogleScholarLink { get; set; }
    }
}