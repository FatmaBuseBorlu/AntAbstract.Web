using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ReviewerDashboardViewModel
    {
        public int TotalAssigned { get; set; }

        public int CompletedReviews { get; set; }

        public int PendingReviews { get; set; }

        public List<ReviewAssignment> PendingAssignments { get; set; }

        public ReviewerDashboardViewModel()
        {
            PendingAssignments = new List<ReviewAssignment>();
        }
    }
}