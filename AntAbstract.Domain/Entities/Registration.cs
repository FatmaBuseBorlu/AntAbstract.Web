using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class Registration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Kaydı yapan kullanıcı
        public string AppUserId { get; set; }
        [ForeignKey("AppUserId")]
        public AppUser AppUser { get; set; }

        // Hangi kongreye kayıt yapıldığı
        public Guid ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]
        public Conference Conference { get; set; }

        // Hangi kayıt tipini seçtiği
        public Guid RegistrationTypeId { get; set; }
        [ForeignKey("RegistrationTypeId")]
        public RegistrationType RegistrationType { get; set; }

        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        // Ödeme durumu
        public bool IsPaid { get; set; } = false;
        public DateTime? PaymentDate { get; set; }

        // Ödeme sağlayıcısından gelen işlem ID'si (örn: Stripe'ın 'charge_id'si)
        public string? PaymentTransactionId { get; set; }
    }
}
