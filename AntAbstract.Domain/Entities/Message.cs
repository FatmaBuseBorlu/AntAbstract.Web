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
        public string Subject { get; set; } 

        [Required]
        public string Content { get; set; } 

        public DateTime SentDate { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false; 

        [Required]
        public string SenderId { get; set; }
        [ForeignKey("SenderId")]
        public AppUser Sender { get; set; }

        [Required]
        public string ReceiverId { get; set; }
        [ForeignKey("ReceiverId")]
        public AppUser Receiver { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}