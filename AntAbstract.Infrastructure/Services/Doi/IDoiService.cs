using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Services.Doi
{
    public interface IDoiService
    {
        DoiMetadataPreview BuildMetadataPreview(Submission submission);

        Task<DoiPreparationResult> PrepareAsync(Guid submissionId);
    }
}
