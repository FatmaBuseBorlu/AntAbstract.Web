using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public enum BidPreference
    {
        WantToReview = 1,
        ConflictOfInterest = 2,
        NotMyField = 3
    }

    public class ReviewBid
    {
        [Key]
        public int Id { get; set; }

        public Guid SubmissionId { get; set; }

        [ForeignKey(nameof(SubmissionId))]
        public Submission Submission { get; set; } = null!;

        public string ReviewerId { get; set; } = string.Empty;

        [ForeignKey(nameof(ReviewerId))]
        public AppUser Reviewer { get; set; } = null!;

        public BidPreference Preference { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
