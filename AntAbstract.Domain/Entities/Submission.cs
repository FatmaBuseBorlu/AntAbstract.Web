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

        // Köprü: SubmissionId istenirse Id ver
        [NotMapped] public Guid SubmissionId => Id;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        // --- ÖZET ---
        [Required]
        public string Abstract { get; set; }

        // Köprü: AbstractText istenirse Abstract'ı kullan
        [NotMapped]
        public string AbstractText
        {
            get => Abstract;
            set => Abstract = value;
        }

        public string Keywords { get; set; }

        // --- TARİHÇE ---
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Köprü: CreatedAt istenirse CreatedDate'i kullan
        [NotMapped]
        public DateTime CreatedAt
        {
            get => CreatedDate;
            set => CreatedDate = value;
        }

        public DateTime? UpdatedDate { get; set; }
        public DateTime? DecisionDate { get; set; }

        // --- DURUM ---
        public SubmissionStatus Status { get; set; } = SubmissionStatus.New;

        // --- EKSİK OLAN DİĞER ALANLAR (Hatalardan Tespit Edilenler) ---
        public bool IsFeedbackGiven { get; set; } // Anket/Geri bildirim durumu

        // Köprü: Eski sistem tek dosya yolu tutuyorsa geçici olarak burayı kullanabiliriz.
        // Veritabanına kaydetmesin ([NotMapped]), hata vermesin diye boş string tutsun.
        [NotMapped]
        public string FilePath { get; set; } = "";

        // --- İLİŞKİLER ---
        public Guid ConferenceId { get; set; }
        public Conference Conference { get; set; }

        public string AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public AppUser Author { get; set; }

        // Köprü: User istenirse Author ver
        [NotMapped] public AppUser User => Author;
        [NotMapped] public string UserId => AuthorId;

        // KOLEKSİYONLAR
        public ICollection<SubmissionAuthor> SubmissionAuthors { get; set; }
        // Submission.cs dosyasının içine bu property'i ekle:
        public ICollection<SubmissionFile> Files { get; set; }
        public ICollection<ReviewAssignment> ReviewAssignments { get; set; }
    }
}