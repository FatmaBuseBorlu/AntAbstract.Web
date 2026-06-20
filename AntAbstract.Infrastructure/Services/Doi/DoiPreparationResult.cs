using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Services.Doi
{
    public sealed class DoiPreparationResult
    {
        public bool Success { get; set; }

        public DoiStatus Status { get; set; }

        public string Message { get; set; } = "";

        public DoiMetadataPreview? Metadata { get; set; }
    }
}
