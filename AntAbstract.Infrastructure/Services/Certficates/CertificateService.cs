using AntAbstract.Application.DTOs;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.Certficates
{
    public class CertificateService : ICertificateService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly PdfCertificateService _pdfCertificateService;
        private readonly EmailOptions _emailOptions;

        public CertificateService(
            AppDbContext context,
            IWebHostEnvironment env,
            IEmailService emailService,
            PdfCertificateService pdfCertificateService,
            IOptions<EmailOptions> emailOptions)
        {
            _context = context;
            _env = env;
            _emailService = emailService;
            _pdfCertificateService = pdfCertificateService;
            _emailOptions = emailOptions.Value;
        }

        public async Task<List<Certificate>> GetMyCertificatesAsync(
            string userId,
            Guid? conferenceId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<Certificate>();
            }

            // Sertifikalarım ekranı slug taşımayan /Certificates adresinden de
            // açılıyor; orada tenant bağlamı boş kaldığı için kiracı filtresi
            // listeyi sessizce boşaltıyordu. Kapsamı userId zaten sağlıyor.
            var query = _context.Certificates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Conference)
                    .ThenInclude(x => x!.Tenant)
                .Where(x => x.UserId == userId);

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                query = query.Where(x => x.ConferenceId == conferenceId.Value);
            }

            return await query
                .OrderByDescending(x => x.GeneratedAt ?? x.EligibleAt)
                .ToListAsync();
        }

        public async Task<byte[]?> GetCertificateFileAsync(
            Guid certificateId,
            string userId)
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

        public Task EnsureAuthorCertificateAsync(
            Guid conferenceId,
            string userId)
        {
            return EnsureCertificateAsync(
                conferenceId,
                userId,
                CertificateType.Author);
        }

        public Task EnsureReviewerCertificateAsync(
            Guid conferenceId,
            string userId)
        {
            return EnsureCertificateAsync(
                conferenceId,
                userId,
                CertificateType.Reviewer);
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
                overrideFullName: reviewerFullName,
                overrideEmail: email);
        }

        public Task EnsureAttendeeCertificateAsync(Guid conferenceId, string userId)
        {
            return EnsureCertificateAsync(
                conferenceId,
                userId,
                CertificateType.Attendee);
        }

        public async Task RegenerateCertificateFileAsync(
            Guid certificateId,
            bool resendEmail = false)
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
                await EnsureAuthorCertificateAsync(
                    certificate.ConferenceId,
                    certificate.UserId);
            }
            else if (certificate.Type == CertificateType.Reviewer)
            {
                await EnsureReviewerCertificateAsync(
                    certificate.ConferenceId,
                    certificate.UserId);
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
                    .ThenInclude(x => x!.Tenant)
                .FirstOrDefaultAsync(x => x.Id == certificateId);

            if (certificate == null)
            {
                return;
            }

            var needsRegeneration =
                certificate.GeneratedAt == null ||
                string.IsNullOrWhiteSpace(certificate.FilePath) ||
                !CertificateFileExists(certificate.FilePath);

            if (needsRegeneration)
            {
                await RegenerateCertificateFileAsync(
                    certificateId,
                    resendEmail: false);

                certificate = await _context.Certificates
                    .Include(x => x.Conference)
                        .ThenInclude(x => x!.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == certificateId);

                if (certificate == null ||
                    certificate.GeneratedAt == null ||
                    string.IsNullOrWhiteSpace(certificate.FilePath) ||
                    !CertificateFileExists(certificate.FilePath))
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

        public byte[] GenerateAcceptanceCertificate(CertificateDataDto data)
        {
            return _pdfCertificateService.GenerateAcceptanceCertificate(data);
        }

        private async Task EnsureCertificateAsync(
            Guid conferenceId,
            string userId,
            CertificateType type,
            string? overrideFullName = null,
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
                CertificateType.Attendee => await IsAttendeeEligibleAsync(conferenceId, userId),
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

            var needsFileGeneration =
                certificate.GeneratedAt == null ||
                string.IsNullOrWhiteSpace(certificate.FilePath) ||
                !CertificateFileExists(certificate.FilePath);

            if (needsFileGeneration)
            {
                await GenerateAndSaveCertificateFileAsync(
                    certificate,
                    conference,
                    overrideFullName);
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

        private async Task<bool> IsAuthorEligibleAsync(
            Guid conferenceId,
            string userId)
        {
            var attendanceCompleted = await _context.ConferenceAttendances
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ConferenceId == conferenceId &&
                    x.UserId == userId &&
                    (
                        x.CompletedAt.HasValue ||
                        x.TotalSeconds >= x.RequiredSeconds
                    ));

            if (!attendanceCompleted)
            {
                return false;
            }

            var isMainAuthor = await _context.Submissions
                .AsNoTracking()
                .AnyAsync(s =>
                    s.ConferenceId == conferenceId &&
                    s.AuthorId == userId &&
                    (
                        s.Status == SubmissionStatus.Accepted ||
                        s.Status == SubmissionStatus.Presented
                    ));

            if (isMainAuthor)
            {
                return true;
            }

            var isCoAuthor = await _context.SubmissionAuthors
                .AsNoTracking()
                .AnyAsync(a =>
                    a.Submission != null &&
                    a.Submission.ConferenceId == conferenceId &&
                    a.AppUserId == userId &&
                    (
                        a.Submission.Status == SubmissionStatus.Accepted ||
                        a.Submission.Status == SubmissionStatus.Presented
                    ));

            return isCoAuthor;
        }

        private async Task<bool> IsReviewerEligibleAsync(
            Guid conferenceId,
            string userId)
        {
            return await _context.ReviewAssignments
                .AsNoTracking()
                .AnyAsync(ra =>
                    ra.ReviewerId == userId &&
                    ra.Submission != null &&
                    ra.Submission.ConferenceId == conferenceId &&
                    ra.Review != null);
        }

        private async Task<bool> IsAttendeeEligibleAsync(
            Guid conferenceId,
            string userId)
        {
            var attendanceCompleted = await _context.ConferenceAttendances
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ConferenceId == conferenceId &&
                    x.UserId == userId &&
                    (x.CompletedAt.HasValue || x.TotalSeconds >= x.RequiredSeconds));

            if (!attendanceCompleted)
            {
                return false;
            }

            return await _context.Registrations
                .AsNoTracking()
                .AnyAsync(r =>
                    r.ConferenceId == conferenceId &&
                    r.AppUserId == userId);
        }

        private async Task GenerateAndSaveCertificateFileAsync(
            Certificate certificate,
            Conference conference,
            string? overrideFullName = null)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == certificate.UserId);

            if (user == null)
            {
                return;
            }

            /*
             * Not:
             * PdfCertificateService şu an AppUser üzerinden isim üretiyor.
             * overrideFullName parametresini şimdilik akışı bozmamak için tuttuk.
             * İleride PdfCertificateService'e özel isim parametresi eklersek burada kullanabiliriz.
             */
            var pdfBytes = _pdfCertificateService.GenerateParticipationCertificate(
                conference,
                user,
                certificate.Type);

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                return;
            }

            var tenantSlug = conference.Tenant?.Slug ?? conference.Slug ?? "tenant";
            var safeTenant = Slugify(tenantSlug);
            var safeUser = Slugify(user.Email ?? user.UserName ?? user.Id);

            var relativeDirectory = Path.Combine(
                "certificates",
                safeTenant,
                conference.Id.ToString());

            var webRoot = GetSafeWebRootPath();
            var absoluteDirectory = Path.Combine(webRoot, relativeDirectory);

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

        private async Task SendCertificateEmailAsync(
            Certificate certificate,
            string email)
        {
            try
            {
                var conference = certificate.Conference;

                if (conference == null)
                {
                    conference = await _context.Conferences
                        .AsNoTracking()
                        .Include(x => x.Tenant)
                        .FirstOrDefaultAsync(x => x.Id == certificate.ConferenceId);
                }

                var slug = conference?.Tenant?.Slug ?? conference?.Slug ?? "";
                var relativePath = string.IsNullOrWhiteSpace(slug)
                    ? $"/Certificates/Download/{certificate.Id}"
                    : $"/{slug}/Certificates/Download/{certificate.Id}";

                var baseUrl = _emailOptions.BaseUrl.TrimEnd('/');
                var downloadLink = string.IsNullOrWhiteSpace(baseUrl)
                    ? relativePath
                    : $"{baseUrl}{relativePath}";

                var (certificateTypeText, subject) = certificate.Type switch
                {
                    CertificateType.Author => ("Yazar Katılım Sertifikası", "Yazar Katılım Sertifikanız Hazır"),
                    CertificateType.Reviewer => ("Hakem Sertifikası", "Hakem Sertifikanız Hazır"),
                    CertificateType.Attendee => ("Katılım Sertifikası", "Katılım Sertifikanız Hazır"),
                    _ => ("Katılım Sertifikası", "Sertifikanız Hazır")
                };

                var conferenceTitle = conference?.Title ?? "Kongre";

                var body =
                    $"<p>Merhaba,</p>" +
                    $"<p><strong>{conferenceTitle}</strong> için <strong>{certificateTypeText}</strong> belgeniz hazırlandı.</p>" +
                    $"<p>Sertifikanızı indirmek için aşağıdaki bağlantıya tıklayabilirsiniz:</p>" +
                    $"<p><a href=\"{downloadLink}\" style=\"background:#0d6efd;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;\">Sertifikamı İndir</a></p>" +
                    $"<p style=\"color:#6c757d;font-size:12px;\">{downloadLink}</p>" +
                    $"<br/><p>Kongre Yönetim Sistemi</p>";

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

        private bool CertificateFileExists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var absolutePath = ResolveSafeWebRootFilePath(relativePath);

            return absolutePath != null && File.Exists(absolutePath);
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

            var webRoot = Path.GetFullPath(GetSafeWebRootPath());
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, normalizedRelativePath));

            if (!fullPath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullPath;
        }

        private string GetSafeWebRootPath()
        {
            if (!string.IsNullOrWhiteSpace(_env.WebRootPath))
            {
                return _env.WebRootPath;
            }

            var fallbackWebRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot");

            Directory.CreateDirectory(fallbackWebRoot);

            return fallbackWebRoot;
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
