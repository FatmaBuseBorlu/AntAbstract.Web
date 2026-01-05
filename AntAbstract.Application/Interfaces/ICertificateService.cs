using AntAbstract.Domain.Entities;

namespace AntAbstract.Application.Interfaces
{
    public interface ICertificateService
    {
        Task EnsureAuthorCertificateAsync(Guid conferenceId, string userId);
        Task EnsureReviewerCertificateAsync(Guid conferenceId, string userId);

        Task<byte[]?> GetCertificateFileAsync(Guid certificateId, string userId);
        Task<List<Certificate>> GetMyCertificatesAsync(string userId, Guid? conferenceId = null);
    }
}
