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
            // Sınırsız: 500'lük sınırda TryWrite fazlasını sessizce düşürüyordu —
            // 500'den çok alıcılı duyuruda/kongre güncellemesinde e-postaların bir
            // kısmı hiç gitmiyordu. Kuyruk öğesi birkaç KB; binlercesi sorun değil.
            // Uygulama yeniden başlarsa bekleyenler yine kaybolur (kalıcı kuyruk ayrı iş).
            _channel = Channel.CreateUnbounded<EmailQueueItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void Enqueue(EmailQueueItem item)
        {
            // Sınırsız kanalda TryWrite yalnızca kanal kapatıldıysa false döner.
            _channel.Writer.TryWrite(item);
        }

        public async Task<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
