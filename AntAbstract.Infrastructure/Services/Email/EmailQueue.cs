using AntAbstract.Application.Interfaces;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Email
{
    /// <summary>
    /// Bellek içi e-posta kuyruğu. Bounded channel ile backpressure sağlar.
    /// </summary>
    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<EmailQueueItem> _channel;

        public EmailQueue()
        {
            _channel = Channel.CreateBounded<EmailQueueItem>(new BoundedChannelOptions(500)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void Enqueue(EmailQueueItem item)
        {
            // TryWrite başarısız olursa (kanal dolu) kaydı sessizce düşürürüz.
            // Üretim ortamında bu yerine kalıcı kuyruk (DB/Redis) tercih edilmeli.
            _channel.Writer.TryWrite(item);
        }

        public async Task<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
