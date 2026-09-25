using System;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Gönderilecek e-postanın kalıcı kaydı (giden kutusu).
    ///
    /// Eskiden e-postalar bellekteki kuyrukta bekliyordu; IIS uygulamayı
    /// yeniden başlatınca (boşta kapanma, deploy, havuz geri dönüşümü)
    /// bekleyenler kayboluyor, SMTP bir an cevap vermezse de tekrar
    /// denenmiyordu. Satır veritabanında kaldığı için gönderim yeniden
    /// başlatmadan sonra kaldığı yerden sürer.
    /// </summary>
    public class EmailOutboxMessage
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(320)]
        public string ToEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string HtmlBody { get; set; } = string.Empty;

        public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;

        public int Attempts { get; set; }

        /// <summary>Bir sonraki deneme zamanı (UTC). Başarısız denemede ileri atılır.</summary>
        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bir gönderici satırı işleme aldığında doldurulur. IIS yeniden başlarken
        /// kısa süre iki süreç birden çalışabiliyor; kilitli satırı diğeri almaz.
        /// Süreç gönderirken çökerse kilit süresi dolunca satır yeniden alınır.
        /// </summary>
        public DateTime? LockedUntil { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        [MaxLength(1000)]
        public string? LastError { get; set; }
    }

    public enum EmailOutboxStatus
    {
        Pending = 0,
        Sent = 1,
        Failed = 2
    }
}
