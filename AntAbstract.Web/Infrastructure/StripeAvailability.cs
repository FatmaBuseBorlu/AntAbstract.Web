namespace AntAbstract.Web.Infrastructure
{
    /// <summary>
    /// Stripe, Türkiye'de kurulu şirketlere hesap açmıyor (stripe.com/global);
    /// Türk organizatörler için kartla ödemenin yolu PayTR. Stripe kodu, yurt
    /// dışı tüzel kişiliği olan bir organizatör için yerinde duruyor ama
    /// platformda Stripe anahtarı girilmediyse yönetim ekranlarında seçenek
    /// olarak hiç gösterilmez — açılsa da ödeme sayfasında çıkmıyordu.
    /// </summary>
    public static class StripeAvailability
    {
        public static bool IsConfigured(IConfiguration configuration)
        {
            var value = configuration["Stripe:SecretKey"];

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            return !trimmed.StartsWith("#{", StringComparison.Ordinal) &&
                   !trimmed.StartsWith("SET_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
