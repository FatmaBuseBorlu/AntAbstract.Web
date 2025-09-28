namespace AntAbstract.Web.Models.ViewModels
{
    public class ReportsViewModel
    {
        public int TotalSubmissions { get; set; }
        public int TotalReviewers { get; set; }
        public int DecidedSubmissions { get; set; }
        public int AwaitingDecisionSubmissions { get; set; }
        public int AcceptedSubmissions { get; set; }
        public int RejectedSubmissions { get; set; }
        public int RevisionSubmissions { get; set; }
        public int AwaitingAssignmentSubmissions { get; set; }
    }
}