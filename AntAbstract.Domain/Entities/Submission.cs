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

            SubmissionIdCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        }

        [Key]
        public Guid Id { get; set; }

        [StringLength(20)]
        public string SubmissionIdCode { get; set; }

        [NotMapped]
        public Guid SubmissionId => Id;

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

        [NotMapped]
        public AppUser User => Author;

        [NotMapped]
        public string UserId => AuthorId;

        [NotMapped]
        public string FilePath { get; set; } = "";

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Abstract { get; set; } = string.Empty;

        public string Keywords { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public DateTime? DecisionDate { get; set; }

        public SubmissionStatus Status { get; set; }

        public bool IsFeedbackGiven { get; set; }

        public Guid ConferenceId { get; set; }

        public virtual Conference Conference { get; set; } = null!;

        public string AuthorId { get; set; } = string.Empty;

        [ForeignKey(nameof(AuthorId))]
        public virtual AppUser Author { get; set; } = null!;

        public Guid TenantId { get; set; }

        public Guid? SessionId { get; set; }

        [ForeignKey(nameof(SessionId))]
        public virtual Session? Session { get; set; }

        /*
         * Yeni alan:
         * Kullanıcının seçtiği bildiri konusu/teması buraya bağlanacak.
         */
        public Guid? ConferenceTopicId { get; set; }

        [ForeignKey(nameof(ConferenceTopicId))]
        public virtual ConferenceTopic? ConferenceTopic { get; set; }

        /*
         * Eski/yedek alan:
         * Seçilen konu adını metin olarak da saklayacağız.
         * Böylece konu adı sonradan değişse bile bildirinin gönderildiği andaki konu metni korunabilir.
         */
        [MaxLength(150)]
        public string Topic { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PresentationType { get; set; } = string.Empty;

        public virtual ICollection<SubmissionAuthor> SubmissionAuthors { get; set; }

        public virtual ICollection<SubmissionFile> Files { get; set; }

        public virtual ICollection<ReviewAssignment> ReviewAssignments { get; set; }
    }
}