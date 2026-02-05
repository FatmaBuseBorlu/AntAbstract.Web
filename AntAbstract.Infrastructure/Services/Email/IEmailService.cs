namespace AntAbstract.Infrastructure.Services.Email
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string htmlMessage);
    }
}
