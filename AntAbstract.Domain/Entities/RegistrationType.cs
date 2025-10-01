using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class RegistrationType
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string Name { get; set; } // Örn: "Öğrenci Kaydı", "Tam Katılım"

        [StringLength(500)]
        public string Description { get; set; } // Örn: "Gala yemeği dahildir."

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // Kayıt ücreti

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "TRY"; // Para birimi (USD, EUR, TRY)

        public Guid ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]
        public Conference Conference { get; set; }
    }
}
