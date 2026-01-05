using System;

namespace AntAbstract.Domain.Entities
{
    public class ConferenceAttendance : BaseEntity
    {
        public Guid ConferenceId { get; set; }
        public Conference? Conference { get; set; }

        public string UserId { get; set; } = "";
        public AppUser? User { get; set; }

        public DateTime FirstJoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastPingAt { get; set; }

        public int TotalSeconds { get; set; } = 0;
        public int RequiredSeconds { get; set; } = 600;

        public DateTime? CompletedAt { get; set; }

        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }

        public bool IsCompleted => CompletedAt.HasValue || TotalSeconds >= RequiredSeconds;
    }
}
