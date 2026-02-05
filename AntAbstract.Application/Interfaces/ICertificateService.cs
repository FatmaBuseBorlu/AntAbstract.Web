using AntAbstract.Application.DTOs;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Application.Interfaces
{
    public interface ICertificateService
    {
        Task<List<Certificate>> GetMyCertificatesAsync(string userId, Guid? conferenceId = null);
        Task<byte[]?> GetCertificateFileAsync(Guid certificateId, string userId);

        Task EnsureAuthorCertificateAsync(Guid conferenceId, string userId);
        Task EnsureReviewerCertificateAsync(Guid conferenceId, string userId);
        Task EnsureReviewerCertificateAsync(Guid conferenceId, string reviewerUserId, string reviewerFullName, string email);

        byte[] GenerateAcceptanceCertificate(CertificateDataDto data);

        Task<byte[]?> GetCertificateFileAdminAsync(Guid certificateId);
        Task RegenerateCertificateFileAsync(Guid certificateId, bool resendEmail = false);
        Task ResendCertificateEmailAsync(Guid certificateId);
    }
}
