using AntAbstract.Domain.Entities;
using System.Threading.Channels;

namespace AntAbstract.Infrastructure.Services
{
    /// <summary>
    /// In-memory audit log kuyruğu.
    /// Singleton olarak kaydedilir; AuditService enqueue eder, AuditWorker tüketir.
    /// Kapasite aşılırsa eski kayıtlar düşürülür (BoundedChannelFullMode.DropOldest)
    /// böylece yüksek yük altında uygulama yavaşlamaz.
    /// </summary>
    public sealed class AuditQueue
    {
        private readonly Channel<AuditLog> _channel;

        public AuditQueue()
        {
            _channel = Channel.CreateBounded<AuditLog>(new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        }

        /// <summary>Kuyruğa ekler. Kapasite doluysa en eski kayıt düşürülür.</summary>
        public bool TryEnqueue(AuditLog entry) =>
            _channel.Writer.TryWrite(entry);

        /// <summary>Kanalda hazır kayıt varsa hemen alır (bloklamamaz).</summary>
        public bool TryRead(out AuditLog? entry) =>
            _channel.Reader.TryRead(out entry);

        /// <summary>BackgroundService'in async foreach döngüsü için.</summary>
        public IAsyncEnumerable<AuditLog> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);

        /// <summary>Uygulama kapanırken çağrılır.</summary>
        public void Complete() => _channel.Writer.Complete();
    }
}
