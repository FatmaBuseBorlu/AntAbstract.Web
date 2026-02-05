using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace AntAbstract.Infrastructure.Services.Email
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;

        public EmailSender(IOptions<EmailOptions> opt)
        {
            _opt = opt.Value;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            using var client = new SmtpClient(_opt.SmtpServer, _opt.Port)
            {
                Credentials = new NetworkCredential(_opt.Username, _opt.Password),
                EnableSsl = true
            };

            var from = new MailAddress(_opt.SenderEmail, _opt.SenderName);
            var msg = new MailMessage(from, new MailAddress(to))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await client.SendMailAsync(msg);
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            throw new NotImplementedException();
        }
    }
}
