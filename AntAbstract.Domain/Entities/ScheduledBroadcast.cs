using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public enum BroadcastStatus
    {
        Pending = 0,
        Sent = 1,
        Failed = 2,
        Cancelled = 3
    }

    public class ScheduledBroadcast
    {
        [Key]
        public int Id { get; set; }

        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public Conference Conference { get; set; } = null!;

        [Required, StringLength(50)]
        public string TargetGroup { get; set; } = "all";

        [Required, StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string HtmlBody { get; set; } = string.Empty;

        public DateTime ScheduledAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        public int RecipientCount { get; set; }

        public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        public string? CreatedByUserId { get; set; }

        [ForeignKey(nameof(CreatedByUserId))]
        public AppUser? CreatedByUser { get; set; }

        public Guid TenantId { get; set; }
    }
}
