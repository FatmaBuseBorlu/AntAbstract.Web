using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AntAbstract.Infrastructure.Services.Email
{
    public class EmailService : IEmailService, Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
    {
        private readonly EmailOptions _emailSettings;

        public EmailService(IOptions<EmailOptions> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public Task SendAsync(string toEmail, string subject, string htmlMessage)
            => SendEmailAsync(toEmail, subject, htmlMessage);

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
    }
}
