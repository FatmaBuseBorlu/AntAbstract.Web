using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Session
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(200)]
        public string Title { get; set; } 

        public DateTime SessionDate { get; set; } 

        [StringLength(100)]
        public string? Location { get; set; }
        public Guid ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]
        public Conference Conference { get; set; }

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
