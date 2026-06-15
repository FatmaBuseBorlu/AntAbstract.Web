using System.Threading;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public record EmailQueueItem(
        string To,
        string Subject,
        string HtmlBody,
        int MaxRetries = 3);

    public interface IEmailQueue
    {
        /// <summary>E-postayı kuyruğa ekler. Non-blocking.</summary>
        void Enqueue(EmailQueueItem item);

        /// <summary>Kuyruktaki bir sonraki e-postayı alır; kuyruk boşsa bekler.</summary>
        Task<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken);
    }
}
