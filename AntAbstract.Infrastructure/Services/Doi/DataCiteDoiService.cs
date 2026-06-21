using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AntAbstract.Infrastructure.Services.Doi
{
    public sealed class DataCiteDoiService : IDoiRegistrationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DataCiteDoiService> _logger;
        private readonly string? _repositoryId;
        private readonly string? _password;
        private readonly string? _prefix;
        private readonly string _baseUrl;
        private readonly string _landingPageBase;

        public DataCiteDoiService(IConfiguration config, IHttpClientFactory httpClientFactory,
            ILogger<DataCiteDoiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _repositoryId = config["Doi:RepositoryId"];
            _password = config["Doi:Password"];
            _prefix = config["Doi:Prefix"];
            _baseUrl = config["Doi:BaseUrl"] ?? "https://api.datacite.org";
            _landingPageBase = config["Doi:LandingPageBaseUrl"] ?? "https://antabstract.com.tr";
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_repositoryId) &&
            !string.IsNullOrWhiteSpace(_password) &&
            !string.IsNullOrWhiteSpace(_prefix) &&
            !_repositoryId!.StartsWith("#{") &&
            !_password!.StartsWith("#{");

        public async Task<DoiRegistrationResult> RegisterAsync(DoiRegistrationRequest request)
        {
            if (!IsConfigured)
                return new DoiRegistrationResult { Success = false, Error = "DataCite servisi yapılandırılmamış." };

            try
            {
                var code = !string.IsNullOrWhiteSpace(request.SubmissionCode)
                    ? request.SubmissionCode
                    : request.SubmissionId.ToString("N")[..8].ToUpperInvariant();

                var slugPart = !string.IsNullOrWhiteSpace(request.Slug)
                    ? $"{request.Slug}.{code}".ToLowerInvariant()
                    : $"submission.{code}".ToLowerInvariant();

                var doi = $"{_prefix}/{slugPart}";

                var landingPage = !string.IsNullOrWhiteSpace(request.Slug)
                    ? $"{_landingPageBase.TrimEnd('/')}/{request.Slug}/Proceedings/Submission/{Uri.EscapeDataString(code)}"
                    : $"{_landingPageBase.TrimEnd('/')}/Proceedings/Submission/{Uri.EscapeDataString(code)}";

                var payload = new
                {
                    data = new
                    {
                        type = "dois",
                        attributes = new
                        {
                            doi,
                            url = landingPage,
                            creators = request.Authors.Select(a => new
                            {
                                name = $"{a.LastName}, {a.FirstName}",
                                givenName = a.FirstName,
                                familyName = a.LastName
                            }),
                            titles = new[] { new { title = request.Title } },
                            publisher = "AntAbstract",
                            publicationYear = request.Year,
                            types = new { resourceTypeGeneral = "ConferencePaper" },
                            schemaVersion = "http://datacite.org/schema/kernel-4"
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var client = _httpClientFactory.CreateClient();
                var authBytes = Encoding.ASCII.GetBytes($"{_repositoryId}:{_password}");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                var response = await client.PostAsync(
                    $"{_baseUrl}/dois",
                    new StringContent(json, Encoding.UTF8, "application/vnd.api+json"));

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("DOI registered: {Doi}", doi);
                    return new DoiRegistrationResult { Success = true, DoiUrl = $"https://doi.org/{doi}" };
                }

                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("DOI registration failed: {Status} {Body}", response.StatusCode, body);
                return new DoiRegistrationResult { Success = false, Error = $"DataCite: {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DOI registration error");
                return new DoiRegistrationResult { Success = false, Error = ex.Message };
            }
        }
    }
}
