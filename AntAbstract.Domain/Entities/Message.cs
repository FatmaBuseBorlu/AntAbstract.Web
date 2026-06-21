using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AntAbstract.Domain;

namespace AntAbstract.Domain.Entities
{
    public class Message
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime SentDate { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        [Required]
        public string SenderId { get; set; } = string.Empty;
        [ForeignKey("SenderId")]
        public AppUser Sender { get; set; } = null!;

        [Required]
        public string ReceiverId { get; set; } = string.Empty;
        [ForeignKey("ReceiverId")]
        public AppUser Receiver { get; set; } = null!;
        public bool IsDeleted { get; set; } = false;

        public Guid? ParentMessageId { get; set; }

        [ForeignKey(nameof(ParentMessageId))]
        public Message? ParentMessage { get; set; }
    }
}
