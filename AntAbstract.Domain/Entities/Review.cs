using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public class Review
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid AssignmentId { get; set; }

        [Range(1, 5)]
        public int? Score { get; set; }

        [StringLength(4000)]
        public string? CommentsToAuthor { get; set; }

        [StringLength(4000)]
        public string? ConfidentialComments { get; set; }

        [StringLength(50)]
        public string? Recommendation { get; set; }

        public DateTime? CompletedDate { get; set; }

        [ForeignKey(nameof(AssignmentId))]
        public ReviewAssignment Assignment { get; set; }
    }
}
