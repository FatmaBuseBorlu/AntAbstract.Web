using System;

namespace AntAbstract.Application.DTOs.Review
{
    public class AssignReviewerDto
    {
        public Guid SubmissionId { get; set; } 
        public string ReviewerId { get; set; } 
    }
}