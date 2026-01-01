using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Web.Models.ViewModels.Admin.RegistrationTypes
{
    public class AdminRegistrationTypeRowModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "TRY";
        public int UsageCount { get; set; }
    }

    public class AdminRegistrationTypesIndexModel
    {
        public string Slug { get; set; } = "";
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = "";

        public List<AdminRegistrationTypeRowModel> Items { get; set; } = new();
    }

    public class AdminRegistrationTypeFormModel
    {
        public string Slug { get; set; } = "";
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = "";

        public Guid? Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(0, 999999999)]
        public decimal Price { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "TRY";

        public string ReturnUrl { get; set; } = "";
    }
}
