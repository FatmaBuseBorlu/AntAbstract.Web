using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AntAbstract.Domain.Entities
{
    public class Reviewer
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string AppUserId { get; set; }

        [Required]
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(AppUserId))]
        public AppUser AppUser { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public Conference Conference { get; set; }

        public string ExpertiseAreas { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
