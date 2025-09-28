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
        public string Title { get; set; } // Oturum Başlığı (Örn: Yapay Zeka ve Etik)

        public DateTime SessionDate { get; set; } // Oturum Tarihi ve Saati

        [StringLength(100)]
        public string? Location { get; set; } // Oturum Yeri (Örn: Salon A, Online Link)

        // Hangi Konferansa ait?
        public Guid ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]
        public Conference Conference { get; set; }

        // Bu oturuma atanan özetlerin listesi
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
