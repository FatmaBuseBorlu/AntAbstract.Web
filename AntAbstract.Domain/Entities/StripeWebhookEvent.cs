using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Stripe'tan gelen webhook olaylarının log kaydı.
    /// </summary>
    public class StripeWebhookEvent
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Stripe'ın evt_xxx event ID'si — tekrar işlemeyi önlemek için.</summary>
        [Required, StringLength(100)]
        public string StripeEventId { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string EventType { get; set; } = string.Empty;

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>processed / skipped / failed</summary>
        [Required, StringLength(20)]
        public string Status { get; set; } = "received";

        /// <summary>İlgili Payment.Id (varsa).</summary>
        public Guid? PaymentId { get; set; }

        /// <summary>Stripe Session ID veya PaymentIntent ID.</summary>
        [StringLength(200)]
        public string? StripeObjectId { get; set; }

        /// <summary>Ham JSON payload (ilk 4000 karakter).</summary>
        [StringLength(4000)]
        public string? PayloadPreview { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>İşlem sürdü mü (idempotency)?</summary>
        public bool IsDuplicate { get; set; } = false;
    }
}
