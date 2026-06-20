using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AntAbstract.Infrastructure.Services.Doi
{
    public sealed class DoiService : IDoiService
    {
        private readonly AppDbContext _context;
        private readonly DoiOptions _options;

        public DoiService(
            AppDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _options = new DoiOptions
            {
                Provider = configuration["Doi:Provider"] ?? "Manual",
                Prefix = configuration["Doi:Prefix"] ?? "",
                LandingPageBaseUrl = configuration["Doi:LandingPageBaseUrl"] ?? ""
            };
        }

        public DoiMetadataPreview BuildMetadataPreview(Submission submission)
        {
            var slug = ResolveSlug(submission);
            var code = !string.IsNullOrWhiteSpace(submission.SubmissionIdCode)
                ? submission.SubmissionIdCode
                : submission.Id.ToString("N")[..8].ToUpperInvariant();

            var missing = GetMissingSettings().ToList();
            var suggestedDoi = missing.Count == 0
                ? $"{NormalizePrefix(_options.Prefix)}/{BuildSuffix(slug, code)}"
                : null;

            return new DoiMetadataPreview
            {
                IsConfigured = missing.Count == 0,
                Provider = HasConfiguredValue(_options.Provider) ? _options.Provider.Trim() : "Manual",
                Prefix = HasConfiguredValue(_options.Prefix) ? NormalizePrefix(_options.Prefix) : null,
                SuggestedDoi = suggestedDoi,
                SuggestedDoiUrl = suggestedDoi == null ? null : $"https://doi.org/{suggestedDoi}",
                LandingUrl = BuildLandingUrl(slug, code),
                Title = submission.Title ?? "",
                ConferenceTitle = submission.Conference?.Title ?? "",
                PublicationDate = submission.Conference?.ProceedingBookPublishedDate
                    ?? submission.DecisionDate
                    ?? submission.CreatedDate,
                Authors = submission.SubmissionAuthors
                    .OrderBy(a => a.Order)
                    .Select(a => $"{a.FirstName} {a.LastName}".Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToList(),
                MissingSettings = missing
            };
        }

        public async Task<DoiPreparationResult> PrepareAsync(Guid submissionId)
        {
            var submission = await _context.Submissions
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(s => s.SubmissionAuthors)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null)
            {
                return new DoiPreparationResult
                {
                    Success = false,
                    Status = DoiStatus.Failed,
                    Message = "Bildiri bulunamadı."
                };
            }

            if (submission.Status != SubmissionStatus.Accepted &&
                submission.Status != SubmissionStatus.Presented)
            {
                return new DoiPreparationResult
                {
                    Success = false,
                    Status = DoiStatus.Failed,
                    Message = "DOI hazırlığı yalnızca kabul edilmiş veya sunuldu durumundaki bildiriler için yapılabilir."
                };
            }

            if (!string.IsNullOrWhiteSpace(submission.DoiUrl))
            {
                submission.DoiStatus = DoiStatus.Assigned;
                submission.DoiAssignedAt ??= DateTime.UtcNow;
                submission.DoiErrorMessage = null;
                await _context.SaveChangesAsync();

                return new DoiPreparationResult
                {
                    Success = true,
                    Status = DoiStatus.Assigned,
                    Metadata = BuildMetadataPreview(submission),
                    Message = "Bu bildiriye DOI daha önce atanmış."
                };
            }

            var metadata = BuildMetadataPreview(submission);
            submission.DoiProvider = metadata.Provider;
            submission.DoiRequestedAt = DateTime.UtcNow;

            if (!metadata.IsConfigured)
            {
                submission.DoiStatus = DoiStatus.ConfigMissing;
                submission.DoiErrorMessage =
                    "Eksik DOI ayarı: " + string.Join(", ", metadata.MissingSettings);

                await _context.SaveChangesAsync();

                return new DoiPreparationResult
                {
                    Success = false,
                    Status = DoiStatus.ConfigMissing,
                    Metadata = metadata,
                    Message = "DOI metadata hazır, ancak provider ayarları eksik."
                };
            }

            submission.DoiStatus = DoiStatus.Ready;
            submission.DoiErrorMessage = null;
            await _context.SaveChangesAsync();

            return new DoiPreparationResult
            {
                Success = true,
                Status = DoiStatus.Ready,
                Metadata = metadata,
                Message = "DOI metadata hazırlandı. Önerilen DOI URL'si kontrol edilip kaydedilebilir."
            };
        }

        private string? BuildLandingUrl(string slug, string code)
        {
            if (!HasConfiguredValue(_options.LandingPageBaseUrl))
                return null;

            var baseUrl = _options.LandingPageBaseUrl.Trim().TrimEnd('/');
            var path = string.IsNullOrWhiteSpace(slug)
                ? $"/Proceedings/Submission/{Uri.EscapeDataString(code)}"
                : $"/{Uri.EscapeDataString(slug)}/Proceedings/Submission/{Uri.EscapeDataString(code)}";

            return baseUrl + path;
        }

        private IEnumerable<string> GetMissingSettings()
        {
            if (!HasConfiguredValue(_options.Prefix))
                yield return "Doi:Prefix";

            if (!HasConfiguredValue(_options.LandingPageBaseUrl))
                yield return "Doi:LandingPageBaseUrl";
        }

        private static string ResolveSlug(Submission submission)
        {
            return submission.Conference?.Tenant?.Slug
                ?? submission.Conference?.Slug
                ?? "";
        }

        private static string NormalizePrefix(string prefix)
        {
            return prefix.Trim().TrimEnd('/');
        }

        private static string BuildSuffix(string slug, string code)
        {
            var raw = string.IsNullOrWhiteSpace(slug)
                ? $"submission.{code}"
                : $"{slug}.{code}";

            var normalized = RemoveDiacritics(raw).ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"[^a-z0-9._-]+", "-");
            normalized = Regex.Replace(normalized, @"-+", "-").Trim('-', '.', '_');

            return string.IsNullOrWhiteSpace(normalized)
                ? code.ToLowerInvariant()
                : normalized;
        }

        private static string RemoveDiacritics(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool HasConfiguredValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            return !trimmed.StartsWith("#{", StringComparison.Ordinal) &&
                   !trimmed.StartsWith("SET_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
