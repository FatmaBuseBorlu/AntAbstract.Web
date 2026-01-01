using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Registrations
{
    public class AdminRegistrationRowModel
    {
        public Guid Id { get; set; }
        public string UserFullName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string RegistrationTypeName { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public bool IsPaid { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime? PaymentDate { get; set; }
    }
    public class RegistrationTypeLookupItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }


    public class AdminRegistrationsIndexModel
    {
        public string Slug { get; set; } = "";
        public Guid? ConferenceId { get; set; }
        public string? ConferenceTitle { get; set; }

        public string? Search { get; set; }
        public string? Paid { get; set; }
        public Guid? RegistrationTypeId { get; set; }
        public List<RegistrationTypeLookupItem> RegistrationTypes { get; set; } = new();


        public List<AdminRegistrationRowModel> Items { get; set; } = new();
    }
    
}
