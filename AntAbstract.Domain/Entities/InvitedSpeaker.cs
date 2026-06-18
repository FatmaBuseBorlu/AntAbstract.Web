using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class InvitedSpeaker
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(300)]
        public string? Institution { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        [StringLength(500)]
        public string? PhotoUrl { get; set; }

        [StringLength(300)]
        public string? TalkTitle { get; set; }

        [StringLength(3000)]
        public string? Bio { get; set; }

        [StringLength(200)]
        public string? WebsiteUrl { get; set; }

        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public virtual Conference Conference { get; set; } = null!;
    }
}
