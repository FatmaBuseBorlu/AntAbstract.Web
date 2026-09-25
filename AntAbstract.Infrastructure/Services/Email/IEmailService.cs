using System.Collections.Generic;

namespace AntAbstract.Infrastructure.Services.Email
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string htmlMessage);

        /// <summary>
        /// DB'deki EmailTemplate kaydını key ile bulur, placeholder'ları değiştirir ve gönderir.
        /// Şablon bulunamazsa veya aktif değilse sessizce atlar.
        /// Placeholder örneği: { "{FullName}", "Ahmet" }
        /// </summary>
        Task SendTemplatedAsync(
            string toEmail,
            string templateKey,
            Dictionary<string, string> placeholders);

        /// <summary>
        /// Şablonu işler ve arka plan kuyruğuna ekler; isteği SMTP'yi beklemeden
        /// bırakır. Şablon yoksa/pasifse false döner. Kullanıcının yazdığı
        /// değerler (ad, başlık) çağıran tarafından HTML-kodlanmalıdır.
        /// </summary>
        Task<bool> EnqueueTemplatedAsync(
            string toEmail,
            string templateKey,
            Dictionary<string, string> placeholders);
    }
}
