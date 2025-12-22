using System;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ConferenceFlowIndexViewModel
    {
        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = "";
        public string Slug { get; set; } = "";

        public int SubmissionCount { get; set; }
        public int AssignedSubmissionCount { get; set; }
        public int DecidedSubmissionCount { get; set; }
    }
}
