namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Kayıt yaşam döngüsü durumları.
    /// </summary>
    public enum RegistrationStatus
    {
        /// <summary>Kayıt oluşturuldu, ödeme bekleniyor.</summary>
        Pending = 0,

        /// <summary>Ödeme makbuzu yüklendi, admin onayı bekleniyor.</summary>
        AwaitingApproval = 1,

        /// <summary>Ödeme onaylandı / Stripe ile tamamlandı.</summary>
        Confirmed = 2,

        /// <summary>Makbuz reddedildi, tekrar yükleme gerekiyor.</summary>
        PaymentRejected = 3,

        /// <summary>Kayıt iptal edildi.</summary>
        Cancelled = 4,
    }
}
