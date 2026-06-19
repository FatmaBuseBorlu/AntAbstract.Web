using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AntAbstract.Infrastructure.Services.Plagiarism
{
    public sealed class PlagiarismService : IPlagiarismService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PlagiarismService> _logger;
        private readonly string? _apiKey;
        private readonly string _baseUrl;

        public PlagiarismService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<PlagiarismService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiKey = config["Plagiarism:ApiKey"];
            _baseUrl = config["Plagiarism:BaseUrl"] ?? "https://api.turnitin.com/api/v1";
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<PlagiarismReport> SubmitForCheckAsync(
            Guid submissionId,
            int submissionFileId,
            string resolvedFilePath,
            string fileName,
            string requestedByUserId,
            Guid tenantId)
        {
            var report = new PlagiarismReport
            {
                SubmissionId = submissionId,
                SubmissionFileId = submissionFileId,
                RequestedByUserId = requestedByUserId,
                TenantId = tenantId,
                Status = PlagiarismStatus.Pending
            };

            if (!IsConfigured)
            {
                report.Status = PlagiarismStatus.Failed;
                report.ErrorMessage = "Plagiarism check servisi yapılandırılmamış.";
                _context.PlagiarismReports.Add(report);
                await _context.SaveChangesAsync();
                return report;
            }

            if (!File.Exists(resolvedFilePath))
            {
                report.Status = PlagiarismStatus.Failed;
                report.ErrorMessage = "Dosya sunucuda bulunamadı.";
                _context.PlagiarismReports.Add(report);
                await _context.SaveChangesAsync();
                return report;
            }

            try
            {
                var externalId = await UploadToProviderAsync(resolvedFilePath, fileName, submissionId);
                report.ExternalId = externalId;
                report.Status = PlagiarismStatus.Processing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plagiarism check başlatılamadı. SubmissionId={Id}", submissionId);
                report.Status = PlagiarismStatus.Failed;
                report.ErrorMessage = ex.Message;
            }

            _context.PlagiarismReports.Add(report);
            await _context.SaveChangesAsync();
            return report;
        }

        public async Task<PlagiarismReport?> RefreshStatusAsync(Guid reportId)
        {
            var report = await _context.PlagiarismReports
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null || report.Status != PlagiarismStatus.Processing)
                return report;

            if (!IsConfigured || string.IsNullOrWhiteSpace(report.ExternalId))
                return report;

            try
            {
                var (score, reportUrl, completed) = await CheckStatusAsync(report.ExternalId);

                if (completed)
                {
                    report.Status = PlagiarismStatus.Completed;
                    report.SimilarityScore = score;
                    report.ReportUrl = reportUrl;
                    report.CompletedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plagiarism status güncellenemedi. ReportId={Id}", reportId);
                report.Status = PlagiarismStatus.Failed;
                report.ErrorMessage = ex.Message;
            }

            await _context.SaveChangesAsync();
            return report;
        }

        public async Task<PlagiarismReport?> GetLatestReportAsync(Guid submissionId)
        {
            return await _context.PlagiarismReports
                .AsNoTracking()
                .Where(r => r.SubmissionId == submissionId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private async Task<string> UploadToProviderAsync(string filePath, string fileName, Guid submissionId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            // Step 1: Create submission
            var createPayload = JsonSerializer.Serialize(new
            {
                owner = submissionId.ToString("N"),
                title = $"Submission {submissionId.ToString("N")[..8].ToUpper()}",
                submitter = new { id = submissionId.ToString("N") }
            });

            var createResponse = await client.PostAsync(
                $"{_baseUrl}/submissions",
                new StringContent(createPayload, Encoding.UTF8, "application/json"));

            createResponse.EnsureSuccessStatusCode();
            var createBody = await createResponse.Content.ReadAsStringAsync();
            using var createDoc = JsonDocument.Parse(createBody);
            var externalId = createDoc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Provider did not return submission ID.");

            // Step 2: Upload file
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            fileContent.Headers.ContentDisposition =
                new ContentDispositionHeaderValue("inline") { FileName = fileName };

            var uploadResponse = await client.PutAsync(
                $"{_baseUrl}/submissions/{externalId}/original",
                fileContent);

            uploadResponse.EnsureSuccessStatusCode();

            // Step 3: Generate similarity report
            var reportPayload = JsonSerializer.Serialize(new
            {
                generation_settings = new
                {
                    search_repositories = new[] { "INTERNET", "SUBMITTED_WORK", "PUBLICATION", "CROSSREF" },
                    auto_exclude_self_matching_scope = "ALL"
                }
            });

            var reportResponse = await client.PutAsync(
                $"{_baseUrl}/submissions/{externalId}/similarity",
                new StringContent(reportPayload, Encoding.UTF8, "application/json"));

            reportResponse.EnsureSuccessStatusCode();

            return externalId;
        }

        private async Task<(int? score, string? reportUrl, bool completed)> CheckStatusAsync(string externalId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await client.GetAsync(
                $"{_baseUrl}/submissions/{externalId}/similarity");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (null, null, false);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var statusEl) &&
                statusEl.GetString() == "COMPLETE")
            {
                var score = root.TryGetProperty("overall_match_percentage", out var scoreEl)
                    ? scoreEl.GetInt32()
                    : (int?)null;

                var reportUrl = root.TryGetProperty("viewer_url", out var urlEl)
                    ? urlEl.GetString()
                    : null;

                return (score, reportUrl, true);
            }

            return (null, null, false);
        }
    }
}
