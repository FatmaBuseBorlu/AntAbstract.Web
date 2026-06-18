using System;

namespace AntAbstract.Web.Models.ViewModels.Admin.Assignment
{
    public class ConferenceFlowIndexViewModel
    {
        public Guid ConferenceId { get; set; }

        public Guid TenantId { get; set; }

        public string ConferenceTitle { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public int SubmissionCount { get; set; }

        public int AssignedSubmissionCount { get; set; }

        public int DecidedSubmissionCount { get; set; }

        public int AcceptedCount { get; set; }

        public int RejectedCount { get; set; }

        public int RegistrationCount { get; set; }

        public int PaidRegistrationCount { get; set; }

        public int ReviewerCount { get; set; }

        public bool IsSubmissionOpen { get; set; }

        public bool IsRegistrationOpen { get; set; }

        public bool IsBiddingOpen { get; set; }

        public bool IsFullTextOpen { get; set; }

        public DateTime ConferenceStartDate { get; set; }

        public DateTime ConferenceEndDate { get; set; }

        public DateTime? AbstractSubmissionDeadline { get; set; }

        public int WaitingAssignmentCount
        {
            get
            {
                var value = SubmissionCount - AssignedSubmissionCount;
                return value < 0 ? 0 : value;
            }
        }

        public int WaitingDecisionCount
        {
            get
            {
                var value = SubmissionCount - DecidedSubmissionCount;
                return value < 0 ? 0 : value;
            }
        }

        public int AssignmentProgressPercent
        {
            get
            {
                if (SubmissionCount <= 0)
                {
                    return 0;
                }

                return (int)Math.Round(AssignedSubmissionCount * 100.0 / SubmissionCount);
            }
        }

        public int DecisionProgressPercent
        {
            get
            {
                if (SubmissionCount <= 0)
                {
                    return 0;
                }

                return (int)Math.Round(DecidedSubmissionCount * 100.0 / SubmissionCount);
            }
        }
    }
}