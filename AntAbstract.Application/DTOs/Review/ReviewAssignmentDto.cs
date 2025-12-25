using System;
using System.Collections.Generic;
using AntAbstract.Application.DTOs.Submission; 

namespace AntAbstract.Application.DTOs.Review
{
    public class ReviewAssignmentDto
    {
        public int Id { get; set; }
        public Guid SubmissionId { get; set; } 
        public string SubmissionTitle { get; set; }
        public string SubmissionAbstract { get; set; }
        public string ConferenceName { get; set; }
        public DateTime AssignedDate { get; set; }
        public bool IsReviewed { get; set; }
        public string ReviewerName { get; set; } 
        public int? Score { get; set; }          
        public string Recommendation { get; set; } 
        public string CommentsToAuthor { get; set; } 

        public int? ReviewId { get; set; }
        public List<SubmissionFileDto> Files { get; set; }
    }
}