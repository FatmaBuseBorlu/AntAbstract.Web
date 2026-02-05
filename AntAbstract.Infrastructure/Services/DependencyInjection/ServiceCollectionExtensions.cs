using AntAbstract.Application.Interfaces;
using AntAbstract.Infrastructure.Services.Certficates;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Infrastructure.Services.Notifications;
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
            // Notifications
            services.AddScoped<INotificationService, NotificationService>();

            // Email (Options + Service)
            services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailService>();

            // Conferences
            services.AddScoped<ISelectedConferenceService, SelectedConferenceService>();

            // Certificates ✅ (ReviewController bununla patlıyordu)
            services.AddScoped<ICertificateService, CertificateService>();

            return services;
        }
    }
}
