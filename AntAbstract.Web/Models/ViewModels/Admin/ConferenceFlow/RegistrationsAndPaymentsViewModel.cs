using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.ConferenceFlow
{
    public class RegistrationsAndPaymentsViewModel
    {
        public Guid ConferenceId { get; set; }

        public string ConferenceTitle { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public int TotalRegistrations { get; set; }

        public int PaidRegistrations { get; set; }

        public int PendingPayments { get; set; }

        public decimal TotalRevenue { get; set; }

        public string Search { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public Guid? RegistrationTypeId { get; set; }

        public List<RegistrationTypeFilterItem> RegistrationTypes { get; set; } = new List<RegistrationTypeFilterItem>();

        public List<RegistrationPaymentRowItem> Items { get; set; } = new List<RegistrationPaymentRowItem>();
    }

    public class RegistrationTypeFilterItem
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string Currency { get; set; } = "TRY";
    }

    public class RegistrationPaymentRowItem
    {
        public Guid RegistrationId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string RegistrationTypeName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "TRY";

        public bool IsPaid { get; set; }

        public DateTime RegistrationDate { get; set; }

        public DateTime? PaymentDate { get; set; }

        public string? PaymentTransactionId { get; set; }

        public string? BillingName { get; set; }

        public string? TaxOffice { get; set; }

        public string? TaxNumber { get; set; }
    }
}