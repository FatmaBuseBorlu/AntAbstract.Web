using System;
using System.Collections.Generic;
using AntAbstract.Application.DTOs.Submission;

namespace AntAbstract.Application.DTOs.Review
{
    public class ReviewAssignmentDto
    {
        public int Id { get; set; }

        public Guid SubmissionId { get; set; }

        public string SubmissionTitle { get; set; } = string.Empty;

        public string SubmissionAbstract { get; set; } = string.Empty;

        public string ConferenceName { get; set; } = string.Empty;

        public DateTime AssignedDate { get; set; }

        public DateTime? EvaluationDate { get; set; }

        public bool IsReviewed { get; set; }

        public string ReviewerName { get; set; } = string.Empty;

        public int? Score { get; set; }

        public string Recommendation { get; set; } = string.Empty;

        public string CommentsToAuthor { get; set; } = string.Empty;

        public int? ReviewId { get; set; }

        public List<SubmissionFileDto> Files { get; set; } = new();

        public string? CertificateFirstSignerName { get; set; }

        public string? CertificateFirstSignerTitle { get; set; }

        public string? CertificateSecondSignerName { get; set; }

        public string? CertificateSecondSignerTitle { get; set; }
    }
}