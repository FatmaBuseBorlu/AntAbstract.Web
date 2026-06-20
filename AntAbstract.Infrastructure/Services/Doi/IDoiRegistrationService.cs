namespace AntAbstract.Infrastructure.Services.Doi
{
    public interface IDoiRegistrationService
    {
        bool IsConfigured { get; }
        Task<DoiRegistrationResult> RegisterAsync(DoiRegistrationRequest request);
    }

    public class DoiRegistrationRequest
    {
        public Guid SubmissionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<DoiRegistrationAuthor> Authors { get; set; } = new();
        public string ConferenceTitle { get; set; } = string.Empty;
        public int Year { get; set; }
        public string? Abstract { get; set; }
    }

    public class DoiRegistrationAuthor
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? OrcidId { get; set; }
        public string? Institution { get; set; }
    }

    public class DoiRegistrationResult
    {
        public bool Success { get; set; }
        public string? DoiUrl { get; set; }
        public string? Error { get; set; }
    }
}
