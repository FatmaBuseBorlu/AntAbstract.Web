using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services
{
    public class DummyEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Şimdilik hiçbir şey yapmıyoruz. Sadece hatayı çözmek için var.
            // İleride buraya gerçek e-posta gönderme kodu yazılabilir.
            return Task.CompletedTask;
        }
    }
}