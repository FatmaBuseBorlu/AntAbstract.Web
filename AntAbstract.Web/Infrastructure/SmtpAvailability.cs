namespace AntAbstract.Web.Infrastructure
{
    /// <summary>
    /// SMTP ayarlı mı? Ayarlı değilken e-posta doğrulaması zorunlu tutulursa
    /// doğrulama e-postası hiç gitmez ve yeni kayıt olan kimse giriş yapamaz
    /// (canlıda ayar dosyası yeniden oluşturulduğunda SMTP boş kaldı ve tam
    /// olarak bu yaşandı). Doğrulama yalnızca SMTP varken zorunlu.
    /// </summary>
    public static class SmtpAvailability
    {
        public static bool IsConfigured(IConfiguration configuration) =>
            Has(configuration["Email:SmtpServer"]) &&
            Has(configuration["Email:Username"]) &&
            Has(configuration["Email:Password"]);

        private static bool Has(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            return !trimmed.StartsWith("#{", StringComparison.Ordinal) &&
                   !trimmed.StartsWith("SET_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
