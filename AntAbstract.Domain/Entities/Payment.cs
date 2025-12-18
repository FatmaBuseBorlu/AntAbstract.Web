using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public AppUser User { get; set; }

        public Guid? ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]
        public Conference Conference { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } 

        public string Currency { get; set; } = "TRY"; 

        public string TransactionId { get; set; }

        public string PaymentType { get; set; } = "CreditCard";

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }

    public enum PaymentStatus
    {
        Pending = 0, 
        Success = 1, 
        Failed = 2,  
        Refunded = 3 
    }
}