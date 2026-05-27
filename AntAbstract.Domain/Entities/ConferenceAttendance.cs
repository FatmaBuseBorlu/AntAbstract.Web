using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class ConferenceAttendance : BaseEntity
    {
        public Guid ConferenceId { get; set; }

        public Conference? Conference { get; set; }

        public string UserId { get; set; } = string.Empty;

        public AppUser? User { get; set; }

        public DateTime FirstJoinedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastPingAt { get; set; }

        public int TotalSeconds { get; set; } = 0;

        public int RequiredSeconds { get; set; } = 600;

        public DateTime? CompletedAt { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        [NotMapped]
        public bool IsCompleted
        {
            get
            {
                return CompletedAt.HasValue || TotalSeconds >= RequiredSeconds;
            }
        }

        public void MarkCompleted()
        {
            if (!CompletedAt.HasValue)
            {
                CompletedAt = DateTime.UtcNow;
            }
        }

        public void UpdatePing(int secondsToAdd)
        {
            if (secondsToAdd <= 0)
            {
                return;
            }

            LastPingAt = DateTime.UtcNow;
            TotalSeconds += secondsToAdd;

            if (TotalSeconds >= RequiredSeconds)
            {
                MarkCompleted();
            }
        }
    }
}