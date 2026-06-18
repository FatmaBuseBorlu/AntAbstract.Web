using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Notification : BaseEntity
    {
        [Key]
        public new int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;

        [Required]
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;
        public string Icon { get; set; } = "fas fa-info-circle";
        public string Color { get; set; } = "primary";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
