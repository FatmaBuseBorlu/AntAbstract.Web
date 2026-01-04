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
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public AppUser User { get; set; }

        [Required]
        public string Title { get; set; }
        public string Message { get; set; }
        public string Link { get; set; }

        public bool IsRead { get; set; } = false;
        public string Icon { get; set; } = "fas fa-info-circle";
        public string Color { get; set; } = "primary";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}