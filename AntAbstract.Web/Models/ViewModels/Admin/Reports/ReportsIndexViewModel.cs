using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Reports
{
    public class FinanceCategoryItem
    {
        public string Label { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public int PaidCount { get; set; }
        public int UnpaidCount { get; set; }
    }

    public class ReportsIndexViewModel
    {
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = string.Empty;
        public string ConferenceName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public int TotalSubmissions { get; set; }
        public int AssignedSubmissions { get; set; }
        public int DecidedSubmissions { get; set; }
        public int TotalAssignments { get; set; }

        public int TotalRegistrations { get; set; }
        public decimal TotalRevenue { get; set; }

        public int NewCount { get; set; }
        public int UnderReviewCount { get; set; }
        public int PendingCount { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public int RevisionRequiredCount { get; set; }

        public int AcceptedSubmissions => AcceptedCount;
        public int PendingSubmissions => PendingCount;
        public int RejectedSubmissions => RejectedCount;

        public int PaidRegistrations { get; set; }
        public int UnpaidRegistrations { get; set; }
        public decimal UnpaidRevenue { get; set; }
        public string RevenueCurrency { get; set; } = "TRY";
        public List<FinanceCategoryItem> FinanceByCategory { get; set; } = new();
    }
}
