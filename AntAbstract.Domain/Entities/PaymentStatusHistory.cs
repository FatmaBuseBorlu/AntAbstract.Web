using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Bir ödemenin durum geçiş geçmişi (Pending → Completed, vb.).
    /// Her Approve / Reject / Cancel işleminde bir satır eklenir.
    /// </summary>
    public class PaymentStatusHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid PaymentId { get; set; }

        [ForeignKey(nameof(PaymentId))]
        public Payment? Payment { get; set; }

        public PaymentStatus? OldStatus { get; set; }

        public PaymentStatus NewStatus { get; set; }

        [StringLength(100)]
        public string? ChangedByUserId { get; set; }

        [StringLength(200)]
        public string? ChangedByUserName { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Note { get; set; }

        /// <summary>Kaynak sistem: "Admin", "Stripe", "System"</summary>
        [StringLength(50)]
        public string Source { get; set; } = "Admin";
    }
}
