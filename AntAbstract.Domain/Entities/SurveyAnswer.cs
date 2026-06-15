using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Konferans memnuniyet anket cevaplarını yapılandırılmış şekilde saklar.
    /// </summary>
    public class SurveyAnswer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // ── Kullanıcı ve bağlam ──────────────────────────────────────────────────

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public AppUser? User { get; set; }

        /// <summary>Anketin ilgili olduğu kongre.</summary>
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public Conference? Conference { get; set; }

        /// <summary>Anket bir bildiriye bağlıysa (opsiyonel).</summary>
        public Guid? SubmissionId { get; set; }

        [ForeignKey(nameof(SubmissionId))]
        public Submission? Submission { get; set; }

        // ── Cevaplar ─────────────────────────────────────────────────────────────

        [MaxLength(2000)]
        public string? Answer1 { get; set; }

        [MaxLength(2000)]
        public string? Answer2 { get; set; }

        [MaxLength(2000)]
        public string? Answer3 { get; set; }

        [MaxLength(2000)]
        public string? Answer4 { get; set; }

        [MaxLength(2000)]
        public string? Answer5 { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
