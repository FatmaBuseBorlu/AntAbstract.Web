using System;
using System.Collections.Generic;

namespace AntAbstract.Application.DTOs.Submission
{
    public class SubmissionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Abstract { get; set; }
        public string Keywords { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? DecisionDate { get; set; }

        public string Status { get; set; }

        public string ConferenceTitle { get; set; }
        public string CorrespondingAuthorName { get; set; }
        public List<SubmissionAuthorDto> Authors { get; set; }

        public List<SubmissionFileDto> Files { get; set; }
    }

}