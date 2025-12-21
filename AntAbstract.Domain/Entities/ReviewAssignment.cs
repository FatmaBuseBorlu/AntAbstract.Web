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

        public Guid SubmissionId { get; set; }

        [ForeignKey(nameof(SubmissionId))]
        public Submission Submission { get; set; }

        public string ReviewerId { get; set; }

        [ForeignKey(nameof(ReviewerId))]
        public AppUser Reviewer { get; set; }

        public Review Review { get; set; }
    }
}
