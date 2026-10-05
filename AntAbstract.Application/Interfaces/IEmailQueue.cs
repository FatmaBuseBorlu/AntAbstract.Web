using System.Collections.Generic;

namespace AntAbstract.Application.Interfaces
{
    public record EmailQueueItem(
        string To,
        string Subject,
        string HtmlBody,
        int MaxRetries = 3);

    /// <summary>
    /// Gönderilecek e-postayı giden kutusuna (veritabanı) yazar; gönderimi arka
    /// plandaki gönderici yapar. Yeniden başlatmada kaybolmaz, hata olursa
    /// tekrar denenir.
    /// </summary>
    public interface IEmailQueue
    {
        void Enqueue(EmailQueueItem item);

        /// <summary>Çok alıcılı gönderimde tek kayıtta yazar.</summary>
        void EnqueueRange(IEnumerable<EmailQueueItem> items)
        {
            foreach (var item in items)
                Enqueue(item);
        }
    }
}
