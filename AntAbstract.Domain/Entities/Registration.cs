using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Registration
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string AppUserId { get; set; } = string.Empty;

        [ForeignKey("AppUserId")]
        public virtual AppUser AppUser { get; set; } = null!;

        [Required]
        public Guid ConferenceId { get; set; }

        [ForeignKey("ConferenceId")]
        public virtual Conference Conference { get; set; } = null!;

        [Required]
        public Guid RegistrationTypeId { get; set; }

        [ForeignKey("RegistrationTypeId")]
        public virtual RegistrationType RegistrationType { get; set; } = null!;

        [Required]
        [Display(Name = "Kayıt Tarihi")]
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "Ödeme Durumu")]
        public bool IsPaid { get; set; } = false; 

        [Display(Name = "Ödeme Tarihi")]
        public DateTime? PaymentDate { get; set; }

        [Display(Name = "Ödeme İşlem ID")]
        public string? PaymentTransactionId { get; set; } 

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Ödenen Tutar")]
        public decimal Amount { get; set; } 


        [StringLength(250)]
        [Display(Name = "Fatura Adı / Kurum Adı")]
        public string? BillingName { get; set; }

        [StringLength(100)]
        [Display(Name = "Vergi Dairesi")]
        public string? TaxOffice { get; set; }

        [StringLength(50)]
        [Display(Name = "Vergi Numarası / T.C. Kimlik No")]
        public string? TaxNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "Fatura Adresi")]
        [DataType(DataType.MultilineText)]
        public string? BillingAddress { get; set; }

        /// <summary>Banka havalesi / EFT makbuzu dosya yolu</summary>
        [StringLength(500)]
        public string? ReceiptFilePath { get; set; }

        /// <summary>Makbuz yükleme tarihi</summary>
        public DateTime? ReceiptUploadedAt { get; set; }

        /// <summary>Yönetici onay notu</summary>
        [StringLength(500)]
        public string? AdminPaymentNote { get; set; }

        /// <summary>
        /// Kayıt yaşam döngüsü durumu.
        /// IsPaid=true iken Status=Confirmed olmalıdır (senkronize tutulur).
        /// </summary>
        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        /// <summary>
        /// QR check-in için benzersiz token. Registration oluşturulurken
        /// otomatik atanır; değişmez.
        /// </summary>
        [StringLength(64)]
        public string QrToken { get; set; } = GenerateQrToken();

        /// <summary>İlk fiziksel check-in zamanı.</summary>
        public DateTime? CheckedInAt { get; set; }

        /// <summary>Check-in yapan görevlinin ID'si.</summary>
        public string? CheckedInByUserId { get; set; }

        private static string GenerateQrToken()
            => Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("/", "_").Replace("+", "-").Replace("=", "").Substring(0, 22);
    }
}
