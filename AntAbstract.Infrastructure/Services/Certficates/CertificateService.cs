using AntAbstract.Application.DTOs;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

namespace AntAbstract.Infrastructure.Services.Certficates
{
    public class CertificateService : ICertificateService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;

        public CertificateService(AppDbContext context, IWebHostEnvironment env, IEmailService emailService)
        {
            _context = context;
            _env = env;
            _emailService = emailService;
        }


        public async Task<List<Certificate>> GetMyCertificatesAsync(string userId, Guid? conferenceId = null)
        {
            var q = _context.Certificates
                .AsNoTracking()
                .Where(x => x.UserId == userId);

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                q = q.Where(x => x.ConferenceId == conferenceId.Value);

            return await q
                .OrderByDescending(x => x.EligibleAt)
                .ToListAsync();
        }

        public async Task<byte[]?> GetCertificateFileAsync(Guid certificateId, string userId)
        {
            var cert = await _context.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == certificateId && x.UserId == userId);

            if (cert == null) return null;
            if (string.IsNullOrWhiteSpace(cert.FilePath)) return null;

            var abs = Path.Combine(
                _env.WebRootPath,
                cert.FilePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (!File.Exists(abs)) return null;
            return await File.ReadAllBytesAsync(abs);
        }

        public Task EnsureAuthorCertificateAsync(Guid conferenceId, string userId)
            => EnsureCertificateAsync(conferenceId, userId, CertificateType.Author);

        public Task EnsureReviewerCertificateAsync(Guid conferenceId, string userId)
            => EnsureCertificateAsync(conferenceId, userId, CertificateType.Reviewer);

        public Task EnsureReviewerCertificateAsync(Guid conferenceId, string reviewerUserId, string reviewerFullName, string email)
            => EnsureCertificateAsync(conferenceId, reviewerUserId, CertificateType.Reviewer, overrideEmail: email);


        public async Task<byte[]?> GetCertificateFileAdminAsync(Guid certificateId)
        {
            var cert = await _context.Certificates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == certificateId);

            if (cert == null) return null;
            if (string.IsNullOrWhiteSpace(cert.FilePath)) return null;

            var abs = Path.Combine(
                _env.WebRootPath,
                cert.FilePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (!File.Exists(abs)) return null;
            return await File.ReadAllBytesAsync(abs);
        }

        public async Task RegenerateCertificateFileAsync(Guid certificateId, bool resendEmail = false)
        {
            var cert = await _context.Certificates.FirstOrDefaultAsync(x => x.Id == certificateId);
            if (cert == null) return;

            cert.GeneratedAt = null;
            cert.FilePath = null;
            cert.FileName = null;
            cert.ContentType = null;
            if (resendEmail)
            {
                cert.EmailSentAt = null;
                cert.LastEmailError = null;
            }

            await _context.SaveChangesAsync();

            if (cert.Type == CertificateType.Author)
                await EnsureAuthorCertificateAsync(cert.ConferenceId, cert.UserId);
            else if (cert.Type == CertificateType.Reviewer)
                await EnsureReviewerCertificateAsync(cert.ConferenceId, cert.UserId);
        }

        public async Task ResendCertificateEmailAsync(Guid certificateId)
        {
            var cert = await _context.Certificates
                .Include(x => x.Conference)
                .ThenInclude(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == certificateId);

            if (cert == null) return;

     
            if (cert.GeneratedAt == null || string.IsNullOrWhiteSpace(cert.FilePath))
            {
                await RegenerateCertificateFileAsync(certificateId, resendEmail: false);

                cert = await _context.Certificates
                    .Include(x => x.Conference)
                    .ThenInclude(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == certificateId);

                if (cert == null) return;
                if (cert.GeneratedAt == null || string.IsNullOrWhiteSpace(cert.FilePath)) return;
            }

            var to = await _context.Users.AsNoTracking()
                .Where(x => x.Id == cert.UserId)
                .Select(x => x.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(to)) return;

            var tenantSlug = cert.Conference?.Tenant?.Slug ?? "";
            var downloadLink = string.IsNullOrWhiteSpace(tenantSlug)
                ? $"/Certificates/Download/{cert.Id}"
                : $"/{tenantSlug}/Certificates/Download/{cert.Id}";

            try
            {
                var subject = cert.Type == CertificateType.Author ? "Yazar Sertifikanız Hazır" : "Hakem Sertifikanız Hazır";
                var body = $"Merhaba,<br/><br/>Sertifikanız hazır. İndirmek için tıklayın:<br/>{downloadLink}<br/><br/>AntAbstract";

                await _emailService.SendAsync(to, subject, body);

                cert.EmailTo = to;
                cert.EmailSentAt = DateTime.UtcNow;
                cert.EmailSendCount += 1;
                cert.LastEmailError = null;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                cert.EmailTo = to;
                cert.EmailSendCount += 1;
                cert.LastEmailError = ex.Message;

                await _context.SaveChangesAsync();
            }
        }


        private async Task EnsureCertificateAsync(Guid conferenceId, string userId, CertificateType type, string? overrideEmail = null)
        {
            if (conferenceId == Guid.Empty) return;
            if (string.IsNullOrWhiteSpace(userId)) return;

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conference == null) return;

            if (type == CertificateType.Author)
            {
                var attendanceOk = await _context.ConferenceAttendances
                    .AsNoTracking()
                    .AnyAsync(x => x.ConferenceId == conferenceId && x.UserId == userId && x.CompletedAt != null);

                if (!attendanceOk) return;

                var isAuthorInConference =
                    await _context.Submissions.AsNoTracking()
                        .AnyAsync(s => s.ConferenceId == conferenceId && s.AuthorId == userId)
                    || await _context.SubmissionAuthors.AsNoTracking()
                        .AnyAsync(a => a.Submission.ConferenceId == conferenceId && a.AppUserId == userId);

                if (!isAuthorInConference) return;
            }

            if (type == CertificateType.Reviewer)
            {
                var reviewerCompletedAny =
                    await (from ra in _context.ReviewAssignments.AsNoTracking()
                           join s in _context.Submissions.AsNoTracking() on ra.SubmissionId equals s.Id
                           where s.ConferenceId == conferenceId
                           where ra.ReviewerId == userId
                           where ra.Review != null
                           select ra.Id).AnyAsync();

                if (!reviewerCompletedAny) return;
            }

            var existing = await _context.Certificates
                .FirstOrDefaultAsync(x => x.ConferenceId == conferenceId && x.UserId == userId && x.Type == type);

            if (existing == null)
            {
                existing = new Certificate
                {
                    ConferenceId = conferenceId,
                    UserId = userId,
                    Type = type,
                    EligibleAt = DateTime.UtcNow
                };

                _context.Certificates.Add(existing);
                await _context.SaveChangesAsync();
            }

            if (existing.GeneratedAt == null || string.IsNullOrWhiteSpace(existing.FilePath))
            {
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
                if (user == null) return;

                var pdfBytes = GenerateParticipationPdf(conference, user, type);

                var tenantSlug = conference.Tenant?.Slug ?? "tenant";
                var safeTenant = Slugify(tenantSlug);
                var safeUser = Slugify(user.Email ?? user.Id);

                var relDir = Path.Combine("certificates", safeTenant, conference.Id.ToString());
                var absDir = Path.Combine(_env.WebRootPath, relDir);
                Directory.CreateDirectory(absDir);

                var fileName = $"{type}_{safeUser}.pdf";
                var absPath = Path.Combine(absDir, fileName);

                await File.WriteAllBytesAsync(absPath, pdfBytes);

                existing.FileName = fileName;
                existing.ContentType = "application/pdf";
                existing.FilePath = "/" + Path.Combine(relDir, fileName).Replace("\\", "/");
                existing.GeneratedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            if (existing.EmailSentAt == null)
            {
                string? to = null;

                if (!string.IsNullOrWhiteSpace(overrideEmail))
                    to = overrideEmail;

                if (string.IsNullOrWhiteSpace(to))
                {
                    to = await _context.Users.AsNoTracking()
                        .Where(x => x.Id == userId)
                        .Select(x => x.Email)
                        .FirstOrDefaultAsync();
                }

                if (string.IsNullOrWhiteSpace(to)) return;

                var tenantSlug = conference.Tenant?.Slug ?? "";
                var downloadLink = string.IsNullOrWhiteSpace(tenantSlug)
                    ? $"/Certificates/Download/{existing.Id}"
                    : $"/{tenantSlug}/Certificates/Download/{existing.Id}";

                try
                {
                    var subject = type == CertificateType.Author ? "Yazar Sertifikanız Hazır" : "Hakem Sertifikanız Hazır";
                    var body =
                        $"Merhaba,<br/><br/>Sertifikanız hazır. İndirmek için tıklayın:<br/>{downloadLink}<br/><br/>AntAbstract";

                    await _emailService.SendAsync(to, subject, body);

                    existing.EmailTo = to;
                    existing.EmailSentAt = DateTime.UtcNow;
                    existing.EmailSendCount += 1;
                    existing.LastEmailError = null;

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    existing.EmailTo = to;
                    existing.EmailSendCount += 1;
                    existing.LastEmailError = ex.Message;

                    await _context.SaveChangesAsync();
                }
            }
        }


        private static byte[] GenerateParticipationPdf(Conference conf, AppUser user, CertificateType type)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var title = type == CertificateType.Author ? "Yazar Katılım Sertifikası" : "Hakem Sertifikası";
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
                fullName = user.UserName ?? user.Email ?? "Kullanıcı";

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(14));

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Text(conf.Title ?? "Konferans").FontSize(22).Bold();
                        col.Item().Text(title).FontSize(18).SemiBold();

                        col.Item().PaddingTop(20).Text(fullName).FontSize(16).Bold();
                        col.Item().Text("Bu sertifika ilgili süreç koşulları sağlandığı için oluşturulmuştur.");

                        col.Item().PaddingTop(20).Text($"Tarih: {DateTime.UtcNow:dd.MM.yyyy}");
                    });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateAcceptanceCertificate(CertificateDataDto data)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Text(data.CongressName ?? "Congress").FontSize(22).Bold();
                        col.Item().Text("CERTIFICATE OF ACCEPTANCE").FontSize(18).SemiBold();

                        col.Item().PaddingTop(15).Text($"Submission ID: {data.SubmissionUniqueId}");
                        col.Item().Text($"Title: \"{data.SubmissionTitle}\"");

                        if (data.Authors != null && data.Authors.Count > 0)
                        {
                            col.Item().PaddingTop(10).Text("Authors:").SemiBold();
                            col.Item().Text(string.Join("; ", data.Authors));
                        }

                        col.Item().PaddingTop(15).Text($"Accepted on: {data.AcceptanceDate:dd MMMM yyyy}");
                        col.Item().Text($"{data.CongressLocation}");
                    });
                });
            }).GeneratePdf();
        }


        private static string Slugify(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            value = Regex.Replace(value, @"\s+", "_");
            value = Regex.Replace(value, @"[^a-z0-9_]+", "");
            if (string.IsNullOrWhiteSpace(value)) value = "x";
            return value;
        }
    }
}
