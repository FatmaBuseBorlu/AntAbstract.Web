using AntAbstract.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Submission : IMustHaveTenant
    {
        public Submission()
        {
            SubmissionAuthors = new HashSet<SubmissionAuthor>();
            Files = new HashSet<SubmissionFile>();
            ReviewAssignments = new HashSet<ReviewAssignment>();
            CreatedDate = DateTime.UtcNow;
            Status = SubmissionStatus.New;
        }

        [Key]
        public Guid Id { get; set; }
        [NotMapped] public Guid SubmissionId => Id;

        [NotMapped]
        public string AbstractText
        {
            get => Abstract;
            set => Abstract = value;
        }

        [NotMapped]
        public DateTime CreatedAt
        {
            get => CreatedDate;
            set => CreatedDate = value;
        }

        [NotMapped] public AppUser User => Author;
        [NotMapped] public string UserId => AuthorId;

        [NotMapped]
        public string FilePath { get; set; } = ""; 

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string Abstract { get; set; }

        public string Keywords { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public DateTime? DecisionDate { get; set; }

        public SubmissionStatus Status { get; set; }

        public bool IsFeedbackGiven { get; set; }

        public Guid ConferenceId { get; set; }
        public virtual Conference Conference { get; set; }

        public string AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public virtual AppUser Author { get; set; }
        public Guid TenantId { get; set; }
        public virtual ICollection<SubmissionAuthor> SubmissionAuthors { get; set; }
        public virtual ICollection<SubmissionFile> Files { get; set; }

        public virtual ICollection<ReviewAssignment> ReviewAssignments { get; set; }
    }
}