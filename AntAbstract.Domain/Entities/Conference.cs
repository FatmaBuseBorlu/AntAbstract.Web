using AntAbstract.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Conference : IMustHaveTenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Title { get; set; } = null!;

        public string? TitleEn { get; set; }

        public string? Description { get; set; }

        public string? DescriptionEn { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string? City { get; set; }

        public string? CityEn { get; set; }

        public string? Country { get; set; }

        public string? CountryEn { get; set; }

        public string? Venue { get; set; }

        public string? VenueEn { get; set; }

        public string? LogoPath { get; set; }

        public string? BannerPath { get; set; }

        [Required]
        [MaxLength(200)]
        public string Slug { get; set; } = null!;

        public string? ExternalWebsiteUrl { get; set; }

        public Guid TenantId { get; set; }

        public Tenant Tenant { get; set; } = null!;

        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

        public string? WritingRulesPath { get; set; }

        public string? AbstractTemplatePath { get; set; }

        public string? FullTextTemplatePath { get; set; }

        // Bildiri başvuru son tarihleri
        /// <summary>Özet (abstract) gönderim son tarihi. Null ise sınır yok.</summary>
        public DateTime? AbstractSubmissionDeadline { get; set; }

        /// <summary>Tam metin gönderim son tarihi. Null ise sınır yok.</summary>
        public DateTime? FullTextSubmissionDeadline { get; set; }

        /// <summary>Bildiri başvuruları açık mı? (Admin elle kapatabilir)</summary>
        public bool IsSubmissionOpen { get; set; } = true;

        // Bildiri kitabı yayında mı?
        public bool IsProceedingBookPublished { get; set; } = false;

        // Bildiri kitabı PDF dosya yolu
        public string? ProceedingBookFilePath { get; set; }

        // Bildiri kitabının yayınlanma tarihi
        public DateTime? ProceedingBookPublishedDate { get; set; }

        /// <summary>Maksimum kayıt sayısı. null = sınırsız.</summary>
        public int? MaxRegistrations { get; set; }

        /// <summary>Kayıt açık mı? (Admin elle kapatabilir)</summary>
        public bool IsRegistrationOpen { get; set; } = true;

        /// <summary>
        /// Katılımcıya kayıt ekranı gösterilmeli mi?
        /// Admin kaydı kapattıysa veya kongrenin tarihi geçtiyse kayıt alınmaz.
        /// Kontenjan kontrolü ayrıca yapılır (veritabanı sayımı gerektirir).
        /// </summary>
        [NotMapped]
        public bool IsRegistrationAvailable =>
            IsRegistrationOpen && EndDate.Date >= DateTime.UtcNow.Date;

        // Sertifika birinci imza bilgileri
        public string? CertificateFirstSignerName { get; set; }

        public string? CertificateFirstSignerTitle { get; set; }

        // Sertifika ikinci imza bilgileri
        public string? CertificateSecondSignerName { get; set; }

        public string? CertificateSecondSignerTitle { get; set; }

        /// <summary>Hakem teklif (bidding) fazı açık mı?</summary>
        public bool IsBiddingOpen { get; set; } = false;

        /// <summary>Tam metin yükleme açık mı? (Admin elle kontrol eder)</summary>
        public bool IsFullTextOpen { get; set; } = false;

        // ── Banka Havalesi Bilgileri ─────────────────────────────────────────
        public string? BankName { get; set; }
        public string? BankIban { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankBranch { get; set; }

        // ── Ödeme Yöntemi Yapılandırması ────────────────────────────────────
        /// <summary>Stripe Checkout etkin mi?</summary>
        public bool IsStripeEnabled { get; set; } = true;
        /// <summary>PayTR etkin mi?</summary>
        public bool IsPayTREnabled { get; set; } = false;
        /// <summary>Banka havalesi etkin mi?</summary>
        public bool IsBankTransferEnabled { get; set; } = false;

        /// <summary>
        /// Kör hakemlik aktif mi? Aktifse hakemler dosya indirdiğinde
        /// PDF metadata'dan yazar bilgileri temizlenir ve dosya adı anonimleştirilir.
        /// </summary>
        public bool IsBlindReview { get; set; } = true;
    }
}