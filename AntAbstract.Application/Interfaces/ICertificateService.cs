using AntAbstract.Application.DTOs;
using AntAbstract.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public interface ICertificateService
    {
        Task<List<Certificate>> GetMyCertificatesAsync(string userId, Guid? conferenceId = null);

        Task<byte[]?> GetCertificateFileAsync(Guid certificateId, string userId);

        Task<byte[]?> GetCertificateFileAdminAsync(Guid certificateId);

        Task EnsureAuthorCertificateAsync(Guid conferenceId, string userId);

        Task EnsureReviewerCertificateAsync(Guid conferenceId, string userId);

        Task EnsureReviewerCertificateAsync(
            Guid conferenceId,
            string reviewerUserId,
            string reviewerFullName,
            string email);

        Task EnsureAttendeeCertificateAsync(Guid conferenceId, string userId);

        byte[] GenerateAcceptanceCertificate(CertificateDataDto data);

        Task RegenerateCertificateFileAsync(Guid certificateId, bool resendEmail = false);

        Task ResendCertificateEmailAsync(Guid certificateId);
    }
}