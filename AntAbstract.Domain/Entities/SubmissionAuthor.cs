using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class SubmissionAuthor
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; } = null!;

        [Required, StringLength(100)]
        public string LastName { get; set; } = null!;

        [StringLength(200)]
        public string? Institution { get; set; }

        [EmailAddress, StringLength(200)]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? ORCID { get; set; }

        public bool IsCorrespondingAuthor { get; set; }
        public int Order { get; set; }

        [Required]
        public Guid SubmissionId { get; set; }

        [ForeignKey(nameof(SubmissionId))]
        public Submission Submission { get; set; } = null!;

        [StringLength(450)]
        public string? AppUserId { get; set; }

        [ForeignKey(nameof(AppUserId))]
        public AppUser? AppUser { get; set; }
    }
}
