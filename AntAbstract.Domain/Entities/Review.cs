using System;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    public class Review
    {
        [Key]
        public int Id { get; set; }

        public int ReviewAssignmentId { get; set; }
        public ReviewAssignment ReviewAssignment { get; set; }

        public string ReviewerName { get; set; }
        public string CommentsToAuthor { get; set; }
        public string Recommendation { get; set; }
        public int Score { get; set; }
        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    }
}
