using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class DashboardViewModel
    {
        // Yazar için
        public List<Submission>? MySubmissions { get; set; }

        // Hakem için
        public List<ReviewAssignment>? MyReviewAssignments { get; set; }

        // Organizatör için
        public int SubmissionsAwaitingAssignment { get; set; }
        public int SubmissionsAwaitingDecision { get; set; }
    }
}