using AntAbstract.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    public class Conference : IMustHaveTenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string? City { get; set; }

        public string? Country { get; set; }

        public string? Venue { get; set; }

        public string? LogoPath { get; set; }

        public string? BannerPath { get; set; }

        public string? Slug { get; set; }

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

        // Sertifika birinci imza bilgileri
        public string? CertificateFirstSignerName { get; set; }

        public string? CertificateFirstSignerTitle { get; set; }

        // Sertifika ikinci imza bilgileri
        public string? CertificateSecondSignerName { get; set; }

        public string? CertificateSecondSignerTitle { get; set; }
    }
}