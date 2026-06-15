using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Email
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public string SmtpServer { get; set; } = "";
        public int Port { get; set; } = 587;

        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        public string SenderName { get; set; } = "";
        public string SenderEmail { get; set; } = "";

        public bool UseSsl { get; set; } = true;

        public string BaseUrl { get; set; } = "";
    }
}
