using System;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Sistemdeki kritik işlemleri kayıt altına alan denetim günlüğü.
    /// </summary>
    public class AuditLog
    {
        public long Id { get; set; }

        /// <summary>İşlemi yapan kullanıcı Id (null = sistem/anonim).</summary>
        public string? UserId { get; set; }

        /// <summary>İşlemi yapan kullanıcı adı (hızlı okuma için denormalize).</summary>
        public string? UserName { get; set; }

        /// <summary>İşlem kategorisi: Login, Submission, Review, Payment, Admin vb.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>İşlem adı: SubmissionCreated, StatusChanged, ReviewSubmitted vb.</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>İşleme konu olan varlık türü (ör. "Submission").</summary>
        public string? EntityType { get; set; }

        /// <summary>İşleme konu olan varlık Id.</summary>
        public string? EntityId { get; set; }

        /// <summary>Eski değer (JSON) — opsiyonel.</summary>
        public string? OldValues { get; set; }

        /// <summary>Yeni değer (JSON) — opsiyonel.</summary>
        public string? NewValues { get; set; }

        /// <summary>Ek serbest metin açıklama.</summary>
        public string? Description { get; set; }

        /// <summary>İlgili kongre Id.</summary>
        public Guid? ConferenceId { get; set; }

        /// <summary>İstemcinin IP adresi.</summary>
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
