using System;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ReportsIndexViewModel
    {
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = "";
        public string Slug { get; set; } = "";
        public int TotalSubmissions { get; set; }
        public int AssignedSubmissions { get; set; }
        public int DecidedSubmissions { get; set; }

        public int NewCount { get; set; }
        public int PendingCount { get; set; }
        public int UnderReviewCount { get; set; }

        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public int RevisionRequiredCount { get; set; }
        public int TotalAssignments { get; set; }
    }
}
