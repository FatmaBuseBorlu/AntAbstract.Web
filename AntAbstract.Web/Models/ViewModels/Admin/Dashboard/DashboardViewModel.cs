using System.Collections.Generic;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Web.Models.ViewModels.Admin.Dashboard
{
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalSubmissions { get; set; }
        public int TotalReviews { get; set; }
        public List<string> ChartLabels { get; set; }
        public List<int> ChartData { get; set; }
        public List<string> TrendLabels { get; set; }
        public List<int> DailyRegistrationCounts { get; set; }
        public List<decimal> DailyPaymentAmounts { get; set; }

        public int AcceptedSubmissions { get; set; }
        public int AwaitingDecision { get; set; }
        public int RejectedSubmissions { get; set; }
        public List<Submission> RecentSubmissions { get; set; } = new();
        public string ConferenceName { get; set; } = string.Empty;
        public List<Conference> ActiveConferences { get; set; } = new();
        public List<Guid> RegisteredConferenceIds { get; set; } = new();

        public List<Conference> MyConferences { get; set; } = new List<Conference>();

        // Admin özgü istatistikler
        public int TotalRegistrations { get; set; }
        public int PendingPayments { get; set; }
        public int ReceiptWaiting { get; set; }
        public int PendingAssignments { get; set; }
        public int TotalReferees { get; set; }
        public decimal TotalRevenue { get; set; }
        public string RevenueCurrency { get; set; } = "TRY";
        public int UnreadNotifications { get; set; }
        public int TotalNotifications24h { get; set; }
        public Conference? SelectedConference { get; set; }

        /// <summary>Kongre yöneticisine gösterilecek eksik yapılandırma uyarıları.</summary>
        public List<ConfigWarning> ConfigWarnings { get; set; } = new();

        public DashboardViewModel()
        {
            ChartLabels = new List<string>();
            ChartData = new List<int>();
            TrendLabels = new List<string>();
            DailyRegistrationCounts = new List<int>();
            DailyPaymentAmounts = new List<decimal>();
        }
    }

    public record ConfigWarning(string Message, string? ActionUrl = null, string? ActionLabel = null);
}
