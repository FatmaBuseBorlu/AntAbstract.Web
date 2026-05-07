using System;

namespace AntAbstract.Web.Models.ViewModels.Admin.Registrations
{
    public class AdminRegistrationDetailsModel
    {
        public Guid Id { get; set; }

        public string Slug { get; set; } = "";

        public Guid ConferenceId { get; set; }

        public string? ConferenceTitle { get; set; }

        public string UserFullName { get; set; } = "";

        public string UserEmail { get; set; } = "";

        public string RegistrationTypeName { get; set; } = "";

        public string? RegistrationTypeNameEn { get; set; }

        public string? RegistrationTypeDescription { get; set; }

        public string? RegistrationTypeDescriptionEn { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "TRY";

        public bool IsPaid { get; set; }

        public DateTime RegistrationDate { get; set; }

        public string? ReturnUrl { get; set; }

        public DateTime? PaymentDate { get; set; }

        public string? PaymentTransactionId { get; set; }
    }
}