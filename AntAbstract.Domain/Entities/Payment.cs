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
        public decimal Amount { get; set; }
        [StringLength(10)]
        public string Currency { get; set; } = "TRY";

        [StringLength(50)]
        public string PaymentMethod { get; set; } // CreditCard, BankTransfer

        [StringLength(150)]
        public string TransactionId { get; set; } // Bankadan dönen işlem no

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public PaymentStatus Status { get; set; } // Enum

        // Fatura Bilgileri
        public string? BillingName { get; set; }
        public string? BillingAddress { get; set; }
        public string? TaxOffice { get; set; }
        public string? TaxNumber { get; set; }

        // İlişkiler
        public string AppUserId { get; set; }
        [ForeignKey("AppUserId")]
        public AppUser AppUser { get; set; }

        public Guid ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]
        public Conference Conference { get; set; }

        // Hangi kayıt/bildiri için?
        public Guid? RelatedSubmissionId { get; set; }
    }

    // ENUM TANIMI (Hata CS0103'ü çözer)
    public enum PaymentStatus
    {
        Pending = 0,
        Completed = 1,
        Failed = 2,
        Refunded = 3
    }
}