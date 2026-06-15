using AntAbstract.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Email
{
    /// <summary>
    /// Arka planda çalışan e-posta gönderici.
    /// Kuyruktaki her item için IEmailService.SendAsync çağırır; hata durumunda
    /// exponential back-off ile MaxRetries kadar yeniden dener.
    /// </summary>
    public class EmailQueueWorker : BackgroundService
    {
        private readonly IEmailQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailQueueWorker> _logger;

        public EmailQueueWorker(
            IEmailQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<EmailQueueWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("E-posta kuyruk işçisi başladı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                EmailQueueItem item;

                try
                {
                    item = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await SendWithRetryAsync(item, stoppingToken);
            }

            _logger.LogInformation("E-posta kuyruk işçisi durdu.");
        }

        private async Task SendWithRetryAsync(EmailQueueItem item, CancellationToken ct)
        {
            for (int attempt = 1; attempt <= item.MaxRetries; attempt++)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendAsync(item.To, item.Subject, item.HtmlBody);
                    _logger.LogDebug("E-posta gönderildi → {To} (deneme {Attempt})", item.To, attempt);
                    return;
                }
                catch (Exception ex) when (attempt < item.MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s...
                    _logger.LogWarning(ex,
                        "E-posta gönderilemedi → {To} (deneme {Attempt}/{Max}). {Delay}s sonra tekrar.",
                        item.To, attempt, item.MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "E-posta gönderilemedi → {To}. Maksimum deneme sayısına ulaşıldı.", item.To);
                }
            }
        }
    }
}
