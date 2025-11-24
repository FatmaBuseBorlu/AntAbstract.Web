using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Submission
    {
        public Guid SubmissionId { get; set; } = Guid.NewGuid();

        [Required]
        public string Title { get; set; }
        public string? AbstractText { get; set; }
        public string? Keywords { get; set; }

        // --- YENİ SİSTEM (ENUM & FILES) ---
        public SubmissionStatus Status { get; set; } = SubmissionStatus.New;
        public ICollection<SubmissionFile> Files { get; set; } = new List<SubmissionFile>();

        // --- ESKİ SİSTEM (GERİ EKLENENLER - HATALARI GİDERMEK İÇİN) ---
        public string? FilePath { get; set; } // Eski kodlar patlamasın diye duruyor
        public string? FinalDecision { get; set; }
        public DateTime? DecisionDate { get; set; }

        // İlişkiler
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string AuthorId { get; set; }
        public AppUser Author { get; set; }

        public ICollection<ReviewAssignment> ReviewAssignments { get; set; } = new List<ReviewAssignment>();

        public Guid? SessionId { get; set; }
        [ForeignKey("SessionId")]
        public Session? Session { get; set; }

        // Eski kodlarda Conference kullanıldığı için geri açtık
        public Guid ConferenceId { get; set; }
        public Conference? Conference { get; set; }
        // Submission.cs içine mevcut property'lerin arasına ekleyin:

        public ICollection<SubmissionAuthor> SubmissionAuthors { get; set; } = new List<SubmissionAuthor>();
        // Not: Diğer collection'lar da (Files, ReviewAssignments) burada olmalıdır.
    }
}