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

        public string ReviewerId { get; set; }

        public AppUser Reviewer { get; set; }

        public Guid SubmissionId { get; set; }

        public Submission Submission { get; set; }

        public int? ReviewId { get; set; }

        public Review Review { get; set; }
    }
}
