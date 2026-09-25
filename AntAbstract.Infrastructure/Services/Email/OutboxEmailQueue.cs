using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;

namespace AntAbstract.Infrastructure.Services.Email
{
    /// <summary>
    /// IEmailQueue'nun kalıcı hâli: e-postalar bellekteki kanal yerine
    /// EmailOutbox tablosuna yazılır (bkz. EmailOutboxMessage). Çağıranın
    /// DbContext'ine karışmamak için kendi kapsamını açar.
    /// </summary>
    public sealed class OutboxEmailQueue : IEmailQueue
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public OutboxEmailQueue(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public void Enqueue(EmailQueueItem item) => EnqueueRange(new[] { item });

        public void EnqueueRange(IEnumerable<EmailQueueItem> items)
        {
            var rows = items
                .Where(i => !string.IsNullOrWhiteSpace(i.To))
                .Select(i => new EmailOutboxMessage
                {
                    ToEmail = i.To.Trim(),
                    Subject = i.Subject.Length > 500 ? i.Subject[..500] : i.Subject,
                    HtmlBody = i.HtmlBody,
                    NextAttemptAt = DateTime.UtcNow
                })
                .ToList();

            if (rows.Count == 0)
                return;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.EmailOutbox.AddRange(rows);
            context.SaveChanges();
        }
    }
}
