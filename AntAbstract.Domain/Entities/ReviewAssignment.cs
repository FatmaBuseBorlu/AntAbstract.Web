using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class ReviewAssignment
    {
        [Key]
        public int Id { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        public DateTime? EvaluationDate { get; set; }

        public Guid SubmissionId { get; set; }

        [ForeignKey(nameof(SubmissionId))]
        public Submission Submission { get; set; } = null!;

        public string ReviewerId { get; set; } = string.Empty;

        [ForeignKey(nameof(ReviewerId))]
        public AppUser Reviewer { get; set; } = null!;

        public Review? Review { get; set; }

        // ── Ret bilgisi ──────────────────────────────────────────────────────────
        /// <summary>Hakem görevi reddettiyse true; kayıt silinmez, saklanır.</summary>
        public bool IsDeclined { get; set; } = false;
        public string? DeclineReason { get; set; }
        public DateTime? DeclinedAt { get; set; }
    }
}
