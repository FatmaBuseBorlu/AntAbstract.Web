using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Payment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Ödeme Bilgileri
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "TRY";

        // CreditCard, BankTransfer
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "CreditCard";

        // Bankadan dönen işlem no (şimdilik boş olabilir)
        [StringLength(150)]
        public string? TransactionId { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // Fatura Bilgileri
        [StringLength(200)]
        public string? BillingName { get; set; }

        [StringLength(500)]
        public string? BillingAddress { get; set; }

        [StringLength(100)]
        public string? TaxOffice { get; set; }

        [StringLength(50)]
        public string? TaxNumber { get; set; }

        // İlişkiler
        [Required]
        public string AppUserId { get; set; } = default!;

        [ForeignKey(nameof(AppUserId))]
        public AppUser? AppUser { get; set; }

        [Required]
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public Conference? Conference { get; set; }

        // Hangi kayıt için
        public Guid? RelatedSubmissionId { get; set; }
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Completed = 1,
        Failed = 2,
        Refunded = 3
    }
}
