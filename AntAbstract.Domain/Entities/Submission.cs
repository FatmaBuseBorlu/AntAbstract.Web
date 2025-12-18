using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Submission
    {
        [Key]
        public Guid Id { get; set; }
        [NotMapped] public Guid SubmissionId => Id;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string Abstract { get; set; }

        [NotMapped]
        public string AbstractText
        {
            get => Abstract;
            set => Abstract = value;
        }

        public string Keywords { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public DateTime CreatedAt
        {
            get => CreatedDate;
            set => CreatedDate = value;
        }

        public DateTime? UpdatedDate { get; set; }
        public DateTime? DecisionDate { get; set; }

        public SubmissionStatus Status { get; set; } = SubmissionStatus.New;

        public bool IsFeedbackGiven { get; set; } 

        [NotMapped]
        public string FilePath { get; set; } = "";

        public Guid ConferenceId { get; set; }
        public Conference Conference { get; set; }

        public string AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public AppUser Author { get; set; }

        [NotMapped] public AppUser User => Author;
        [NotMapped] public string UserId => AuthorId;

        public ICollection<SubmissionAuthor> SubmissionAuthors { get; set; }
        public ICollection<SubmissionFile> Files { get; set; }
        public ICollection<ReviewAssignment> ReviewAssignments { get; set; }
    }
}