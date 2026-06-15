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

        public int AcceptedSubmissions { get; set; }
        public int AwaitingDecision { get; set; }
        public int RejectedSubmissions { get; set; }
        public List<Submission> RecentSubmissions { get; set; }
        public string ConferenceName { get; set; }
        public List<Conference> ActiveConferences { get; set; }
        public List<Guid> RegisteredConferenceIds { get; set; }

        public List<Conference> MyConferences { get; set; } = new List<Conference>();

        // Admin özgü istatistikler
        public int TotalRegistrations { get; set; }
        public int PendingPayments { get; set; }
        public int ReceiptWaiting { get; set; }
        public int PendingAssignments { get; set; }   // Henüz hakeme atanmamış bildiri
        public int TotalReferees { get; set; }
        public decimal TotalRevenue { get; set; }
        public string RevenueCurrency { get; set; } = "TRY";
        public Conference? SelectedConference { get; set; }

        public DashboardViewModel()
        {
            ChartLabels = new List<string>();
            ChartData = new List<int>();
        }
    }
}