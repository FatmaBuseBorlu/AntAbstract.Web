using AntAbstract.Application.Interfaces;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Infrastructure.Services.Certficates;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.Doi;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Infrastructure.Services.Notifications;
using AntAbstract.Infrastructure.Services.Pdf;
using AntAbstract.Infrastructure.Services.Plagiarism;
using AntAbstract.Infrastructure.Services.ReviewerRecommendation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AntAbstract.Infrastructure.Services.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment env)
        {
            services.AddScoped<INotificationService, NotificationService>();

            services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailService>();

            services.AddScoped<ISelectedConferenceService, SelectedConferenceService>();

            services.AddScoped<ICertificateService, CertificateService>();

            services.AddScoped<IReviewerRecommendationService, ReviewerRecommendationService>();
            services.AddScoped<IConferencePageBlockService, ConferencePageBlockService>();
            services.AddScoped<IDoiService, DoiService>();

            // Audit log kuyruğu + background worker
            services.AddSingleton<AuditQueue>();
            services.AddSingleton<IAuditService, AuditService>();
            services.AddHostedService<AuditWorker>();

            // E-posta giden kutusu (veritabanı) + gönderici: yeniden başlatmada
            // kaybolmaz, hata olursa tekrar dener, dakika başına sınırlı gönderir.
            services.AddSingleton<IEmailQueue, OutboxEmailQueue>();

            // Testte gönderici elle çalıştırılır: 5 sn'de bir veritabanına bakan
            // arka plan kopyası paylaşılan test bağlantısıyla yarışırdı.
            if (!env.IsEnvironment("Testing"))
                services.AddHostedService<OutboxEmailWorker>();

            // Zamanlanmış mail hatırlatıcıları (deadline, ödeme bekleyen)
            services.AddHostedService<MailReminderWorker>();

            // Zamanlanmış toplu e-posta gönderimi
            services.AddHostedService<BroadcastWorker>();

            services.AddSingleton<IPdfAnonymizer, PdfAnonymizer>();
            services.AddScoped<IPlagiarismService, PlagiarismService>();
            services.AddScoped<IDoiRegistrationService, DataCiteDoiService>();

            return services;
        }
    }
}
