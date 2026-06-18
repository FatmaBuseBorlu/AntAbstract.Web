using System;

namespace AntAbstract.Domain.Entities
{
    public enum InvitationStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Expired = 3
    }

    public class ReviewerInvitation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConferenceId { get; set; }
        public Conference Conference { get; set; } = null!;

        /// <summary>Davet edilen kişinin e-posta adresi</summary>
        public string Email { get; set; } = null!;

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Institution { get; set; }

        /// <summary>Eğer sistemde kayıtlı bir kullanıcıya davet gönderildiyse</summary>
        public string? InvitedUserId { get; set; }
        public AppUser? InvitedUser { get; set; }

        public string Token { get; set; } = null!;

        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RespondedAt { get; set; }

        /// <summary>Reddetme / iptal notu</summary>
        public string? DeclineReason { get; set; }

        /// <summary>Davet kabul edildikten sonra oluşturulan hakem kullanıcı ID'si</summary>
        public string? CreatedReviewerUserId { get; set; }
    }
}
