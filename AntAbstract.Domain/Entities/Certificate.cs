using System;

namespace AntAbstract.Domain.Entities
{
    public enum CertificateType
    {
        Author = 1,
        Reviewer = 2
    }

    public class Certificate : BaseEntity
    {
        public Guid ConferenceId { get; set; }
        public Conference? Conference { get; set; }

        public string UserId { get; set; } = "";
        public AppUser? User { get; set; }

        public CertificateType Type { get; set; }

        public DateTime EligibleAt { get; set; } = DateTime.UtcNow;

        public DateTime? GeneratedAt { get; set; }
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; } = "application/pdf";

        public DateTime? EmailSentAt { get; set; }
        public string? EmailTo { get; set; }
        public int EmailSendCount { get; set; } = 0;
        public string? LastEmailError { get; set; }

        public string? CertificateNo { get; set; }
    }
}
