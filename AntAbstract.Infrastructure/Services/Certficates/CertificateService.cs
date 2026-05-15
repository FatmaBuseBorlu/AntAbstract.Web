using AntAbstract.Application.DTOs;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AntAbstract.Infrastructure.Services.Certficates
{
    public class CertificateService : ICertificateService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly PdfCertificateService _pdfCertificateService;

        public CertificateService(
            AppDbContext context,
            IWebHostEnvironment env,
            IEmailService emailService,
            PdfCertificateService pdfCertificateService)
        {
            _context = context;
            _env = env;
            _emailService = emailService;
            _pdfCertificateService = pdfCertificateService;
        }

        public async Task<List<Certificate>> GetMyCertificatesAsync(string userId, Guid? conferenceId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<Certificate>();
            }

            var query = _context.Certificates
                .AsNoTracking()
                .Include(x => x.Conference)
                .Where(x => x.UserId == userId);

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                query = query.Where(x => x.ConferenceId == conferenceId.Value);
            }

            return await query
                .OrderByDescending(x => x.EligibleAt)
                .ToListAsync();
        }

        public async Task<byte[]?> GetCertificateFileAsync(Guid certificateId, string userId)
        {
            if (certificateId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var certificate = await _context.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == certificateId &&
                    x.UserId == userId);

            if (certificate == null || string.IsNullOrWhiteSpace(certificate.FilePath))
            {
                return null;
            }

            var absolutePath = ResolveSafeWebRootFilePath(certificate.FilePath);

            if (absolutePath == null || !File.Exists(absolutePath))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(absolutePath);
        }

        public async Task<byte[]?> GetCertificateFileAdminAsync(Guid certificateId)
        {
            if (certificateId == Guid.Empty)
            {
                return null;
            }

            var certificate = await _context.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == certificateId);

            if (certificate == null || string.IsNullOrWhiteSpace(certificate.FilePath))
            {
                return null;
            }

            var absolutePath = ResolveSafeWebRootFilePath(certificate.FilePath);

            if (absolutePath == null || !File.Exists(absolutePath))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(absolutePath);
        }

        public Task EnsureAuthorCertificateAsync(Guid conferenceId, string userId)
        {
            return EnsureCertificateAsync(conferenceId, userId, CertificateType.Author);
        }

        public Task EnsureReviewerCertificateAsync(Guid conferenceId, string userId)
        {
            return EnsureCertificateAsync(conferenceId, userId, CertificateType.Reviewer);
        }

        public Task EnsureReviewerCertificateAsync(
            Guid conferenceId,
            string reviewerUserId,
            string reviewerFullName,
            string email)
        {
            return EnsureCertificateAsync(
                conferenceId,
                reviewerUserId,
                CertificateType.Reviewer,
                overrideEmail: email);
        }

        public async Task RegenerateCertificateFileAsync(Guid certificateId, bool resendEmail = false)
        {
            if (certificateId == Guid.Empty)
            {
                return;
            }

            var certificate = await _context.Certificates
                .FirstOrDefaultAsync(x => x.Id == certificateId);

            if (certificate == null)
            {
                return;
            }

            certificate.GeneratedAt = null;
            certificate.FilePath = null;
            certificate.FileName = null;
            certificate.ContentType = null;

            if (resendEmail)
            {
                certificate.EmailSentAt = null;
                certificate.LastEmailError = null;
            }

            await _context.SaveChangesAsync();

            if (certificate.Type == CertificateType.Author)
            {
                await EnsureAuthorCertificateAsync(certificate.ConferenceId, certificate.UserId);
            }
            else if (certificate.Type == CertificateType.Reviewer)
            {
                await EnsureReviewerCertificateAsync(certificate.ConferenceId, certificate.UserId);
            }
        }

        public async Task ResendCertificateEmailAsync(Guid certificateId)
        {
            if (certificateId == Guid.Empty)
            {
                return;
            }

            var certificate = await _context.Certificates
                .Include(x => x.Conference)
                    .ThenInclude(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == certificateId);

            if (certificate == null)
            {
                return;
            }

            if (certificate.GeneratedAt == null || string.IsNullOrWhiteSpace(certificate.FilePath))
            {
                await RegenerateCertificateFileAsync(certificateId, resendEmail: false);

                certificate = await _context.Certificates
                    .Include(x => x.Conference)
                        .ThenInclude(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == certificateId);

                if (certificate == null ||
                    certificate.GeneratedAt == null ||
                    string.IsNullOrWhiteSpace(certificate.FilePath))
                {
                    return;
                }
            }

            var email = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == certificate.UserId)
                .Select(x => x.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            await SendCertificateEmailAsync(certificate, email);
        }

        private async Task EnsureCertificateAsync(
            Guid conferenceId,
            string userId,
            CertificateType type,
            string? overrideEmail = null)
        {
            if (conferenceId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conference == null)
            {
                return;
            }

            var isEligible = type switch
            {
                CertificateType.Author => await IsAuthorEligibleAsync(conferenceId, userId),
                CertificateType.Reviewer => await IsReviewerEligibleAsync(conferenceId, userId),
                _ => false
            };

            if (!isEligible)
            {
                return;
            }

            var certificate = await _context.Certificates
                .FirstOrDefaultAsync(x =>
                    x.ConferenceId == conferenceId &&
                    x.UserId == userId &&
                    x.Type == type);

            if (certificate == null)
            {
                certificate = new Certificate
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = conferenceId,
                    UserId = userId,
                    Type = type,
                    EligibleAt = DateTime.UtcNow
                };

                _context.Certificates.Add(certificate);
                await _context.SaveChangesAsync();
            }

            if (certificate.GeneratedAt == null || string.IsNullOrWhiteSpace(certificate.FilePath))
            {
                await GenerateAndSaveCertificateFileAsync(certificate, conference);
            }

            if (certificate.EmailSentAt == null)
            {
                var email = overrideEmail;

                if (string.IsNullOrWhiteSpace(email))
                {
                    email = await _context.Users
                        .AsNoTracking()
                        .Where(x => x.Id == userId)
                        .Select(x => x.Email)
                        .FirstOrDefaultAsync();
                }

                if (!string.IsNullOrWhiteSpace(email))
                {
                    await SendCertificateEmailAsync(certificate, email);
                }
            }
        }

        private async Task<bool> IsAuthorEligibleAsync(Guid conferenceId, string userId)
        {
            var attendanceCompleted = await _context.ConferenceAttendances
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ConferenceId == conferenceId &&
                    x.UserId == userId &&
                    x.CompletedAt != null);

            if (!attendanceCompleted)
            {
                return false;
            }

            var isMainAuthor = await _context.Submissions
                .AsNoTracking()
                .AnyAsync(s =>
                    s.ConferenceId == conferenceId &&
                    s.AuthorId == userId);

            if (isMainAuthor)
            {
                return true;
            }

            var isCoAuthor = await _context.SubmissionAuthors
                .AsNoTracking()
                .AnyAsync(a =>
                    a.Submission != null &&
                    a.Submission.ConferenceId == conferenceId &&
                    a.AppUserId == userId);

            return isCoAuthor;
        }

        private async Task<bool> IsReviewerEligibleAsync(Guid conferenceId, string userId)
        {
            var completedReviewExists = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => ra.ReviewerId == userId)
                .Join(
                    _context.Submissions.AsNoTracking(),
                    ra => ra.SubmissionId,
                    s => s.Id,
                    (ra, s) => new
                    {
                        Assignment = ra,
                        Submission = s
                    })
                .AnyAsync(x =>
                    x.Submission.ConferenceId == conferenceId &&
                    x.Assignment.Review != null);

            return completedReviewExists;
        }

        private async Task GenerateAndSaveCertificateFileAsync(
            Certificate certificate,
            Conference conference)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == certificate.UserId);

            if (user == null)
            {
                return;
            }

            var pdfBytes = _pdfCertificateService.GenerateParticipationCertificate(
                conference,
                user,
                certificate.Type);

            var tenantSlug = conference.Tenant?.Slug ?? "tenant";
            var safeTenant = Slugify(tenantSlug);
            var safeUser = Slugify(user.Email ?? user.Id);

            var relativeDirectory = Path.Combine(
                "certificates",
                safeTenant,
                conference.Id.ToString());

            var absoluteDirectory = Path.Combine(_env.WebRootPath, relativeDirectory);

            Directory.CreateDirectory(absoluteDirectory);

            var fileName = $"{certificate.Type}_{safeUser}_{certificate.Id}.pdf";
            var absolutePath = Path.Combine(absoluteDirectory, fileName);

            await File.WriteAllBytesAsync(absolutePath, pdfBytes);

            certificate.FileName = fileName;
            certificate.ContentType = "application/pdf";
            certificate.FilePath = "/" + Path.Combine(relativeDirectory, fileName).Replace("\\", "/");
            certificate.GeneratedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private async Task SendCertificateEmailAsync(Certificate certificate, string email)
        {
            try
            {
                var downloadLink = $"/Certificates/Download/{certificate.Id}";

                var subject = certificate.Type == CertificateType.Author
                    ? "Yazar Sertifikanız Hazır"
                    : "Hakem Sertifikanız Hazır";

                var body =
                    $"Merhaba,<br/><br/>" +
                    $"Sertifikanız hazır. İndirmek için tıklayın:<br/>" +
                    $"{downloadLink}<br/><br/>" +
                    $"AntAbstract";

                await _emailService.SendAsync(email, subject, body);

                certificate.EmailTo = email;
                certificate.EmailSentAt = DateTime.UtcNow;
                certificate.EmailSendCount += 1;
                certificate.LastEmailError = null;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                certificate.EmailTo = email;
                certificate.EmailSendCount += 1;
                certificate.LastEmailError = ex.Message;

                await _context.SaveChangesAsync();
            }
        }

        private string? ResolveSafeWebRootFilePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var normalizedRelativePath = relativePath
                .TrimStart('/', '\\')
                .Replace("/", Path.DirectorySeparatorChar.ToString())
                .Replace("\\", Path.DirectorySeparatorChar.ToString());

            var webRoot = Path.GetFullPath(_env.WebRootPath);
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, normalizedRelativePath));

            if (!fullPath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullPath;
        }

        public byte[] GenerateAcceptanceCertificate(CertificateDataDto data)
        {
            return _pdfCertificateService.GenerateAcceptanceCertificate(data);
        }

        private static string Slugify(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            value = Regex.Replace(value, @"\s+", "_");
            value = Regex.Replace(value, @"[^a-z0-9_]+", "");

            if (string.IsNullOrWhiteSpace(value))
            {
                value = "x";
            }

            return value;
        }
    }
}