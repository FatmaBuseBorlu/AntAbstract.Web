using AntAbstract.Infrastructure.Context;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Email
{
    public class EmailService : IEmailService, Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
    {
        private readonly EmailOptions _emailSettings;
        private readonly AppDbContext _context;

        public EmailService(IOptions<EmailOptions> emailSettings, AppDbContext context)
        {
            _emailSettings = emailSettings.Value;
            _context = context;
        }

        public Task SendAsync(string toEmail, string subject, string htmlMessage)
            => SendEmailAsync(toEmail, subject, htmlMessage);

        public async Task SendTemplatedAsync(
            string toEmail,
            string templateKey,
            Dictionary<string, string> placeholders)
        {
            var template = await _context.EmailTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Key == templateKey && t.IsActive);

            if (template == null)
            {
                return; // Şablon tanımlı değil, sessizce atla
            }

            var subject = ApplyPlaceholders(template.Subject, placeholders);
            var body    = ApplyPlaceholders(template.HtmlBody, placeholders);

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var email = new MimeMessage();
            email.Sender = new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail);
            email.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlMessage };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        private static string ApplyPlaceholders(string text, Dictionary<string, string> placeholders)
        {
            foreach (var kv in placeholders)
            {
                text = text.Replace(kv.Key, kv.Value, System.StringComparison.OrdinalIgnoreCase);
            }
            return text;
        }
    }
}
