using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Sistem tarafından gönderilen her e-postanın log kaydı.
    /// </summary>
    public class EmailLog
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(320)]
        public string ToEmail { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>sent / failed</summary>
        [Required, StringLength(20)]
        public string Status { get; set; } = "sent";

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>SendTemplatedAsync kullandıysa şablon anahtarı.</summary>
        [StringLength(100)]
        public string? TemplateKey { get; set; }

        /// <summary>Tetikleyen kaynak (ör: "PaymentApproved", "SubmissionAccepted").</summary>
        [StringLength(100)]
        public string? Source { get; set; }
    }
}
