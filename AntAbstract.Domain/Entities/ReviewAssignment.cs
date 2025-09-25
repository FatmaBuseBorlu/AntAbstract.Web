using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public class ReviewAssignment
    {
        [Key]
        public Guid ReviewAssignmentId { get; set; }

        [Required]
        public Guid SubmissionId { get; set; }

        [Required]
        public Guid ReviewerId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "New";

        [ForeignKey(nameof(SubmissionId))]
        public Submission Submission { get; set; }

        [ForeignKey(nameof(ReviewerId))]
        public Reviewer Reviewer { get; set; }

        public Review Review { get; set; }
    }
}
