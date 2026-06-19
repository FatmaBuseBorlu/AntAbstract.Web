using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Services.Plagiarism
{
    public interface IPlagiarismService
    {
        bool IsConfigured { get; }

        Task<PlagiarismReport> SubmitForCheckAsync(
            Guid submissionId,
            int submissionFileId,
            string resolvedFilePath,
            string fileName,
            string requestedByUserId,
            Guid tenantId);

        Task<PlagiarismReport?> RefreshStatusAsync(Guid reportId);

        Task<PlagiarismReport?> GetLatestReportAsync(Guid submissionId);
    }
}
