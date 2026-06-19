using System;
using System.Collections.Generic;

namespace AntAbstract.Application.DTOs.Submission
{
    public class SubmissionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Abstract { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }
        public DateTime? DecisionDate { get; set; }

        public string Status { get; set; } = string.Empty;
        public string? AdminDecisionNote { get; set; }
        public string? RebuttalText { get; set; }
        public DateTime? RebuttalDate { get; set; }

        public Guid ConferenceId { get; set; }
        public string ConferenceTitle { get; set; } = string.Empty;
        public string CorrespondingAuthorName { get; set; } = string.Empty;
        public List<SubmissionAuthorDto> Authors { get; set; } = new();

        public List<SubmissionFileDto> Files { get; set; } = new();
        public string Topic { get; set; } = string.Empty;
        public string PresentationType { get; set; } = string.Empty;
        public string? DoiUrl { get; set; }
    }

}
