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
        [ForeignKey("ReviewerId")]
        public AppUser Reviewer { get; set; }


        public Guid SubmissionId { get; set; }
        [ForeignKey("SubmissionId")]
        public Submission Submission { get; set; }

        public int? ReviewId { get; set; }
        [ForeignKey("ReviewId")]
        public Review Review { get; set; }
    }
}