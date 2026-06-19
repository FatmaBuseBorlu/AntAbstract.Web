using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public enum PlagiarismStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }

    public class PlagiarismReport
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid SubmissionId { get; set; }

        [ForeignKey(nameof(SubmissionId))]
        public virtual Submission Submission { get; set; } = null!;

        public int? SubmissionFileId { get; set; }

        [ForeignKey(nameof(SubmissionFileId))]
        public virtual SubmissionFile? SubmissionFile { get; set; }

        [StringLength(200)]
        public string? ExternalId { get; set; }

        public PlagiarismStatus Status { get; set; } = PlagiarismStatus.Pending;

        [Range(0, 100)]
        public int? SimilarityScore { get; set; }

        [StringLength(500)]
        public string? ReportUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        [StringLength(50)]
        public string Provider { get; set; } = "iThenticate";

        public string? RequestedByUserId { get; set; }

        [ForeignKey(nameof(RequestedByUserId))]
        public virtual AppUser? RequestedByUser { get; set; }

        public Guid TenantId { get; set; }
    }
}
