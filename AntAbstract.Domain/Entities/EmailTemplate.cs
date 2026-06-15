using System;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Admin panelinden düzenlenebilen email şablonları.
    /// Key ile benzersiz şekilde tanımlanır; EmailService bu key ile şablonu çeker.
    /// Placeholder'lar: {FullName}, {ConferenceTitle}, {SubmissionTitle}, {StatusLabel}, {Note}, {DownloadLink}
    /// </summary>
    public class EmailTemplate
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Benzersiz anahtar: "decision.accept", "decision.reject", "certificate.author" vs.</summary>
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>Admin panelinde görünen açıklama</summary>
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string HtmlBody { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
