using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Pdf;
using AntAbstract.Web.Files;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    /// <summary>
    /// Hassas dosyaları (bildiri, makbuz) yetki kontrolüyle serve eder.
    /// Tenant izolasyonu: Admin, DB'deki kendi TenantId'si üzerinden doğrulanır;
    /// route'ta slug olup olmamasından bağımsız çalışır.
    /// </summary>
    [Authorize]
    public class SecureDownloadController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IAuditService _audit;
        private readonly ILogger<SecureDownloadController> _logger;
        private readonly IPdfAnonymizer _pdfAnonymizer;

        public SecureDownloadController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IAdminTenantAccessService tenantAccess,
            IAuditService audit,
            ILogger<SecureDownloadController> logger,
            IPdfAnonymizer pdfAnonymizer)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _tenantAccess = tenantAccess;
            _audit = audit;
            _logger = logger;
            _pdfAnonymizer = pdfAnonymizer;
        }

        // ── Bildiri Dosyası ──────────────────────────────────────────────────────

        [HttpGet("/download/submission/{fileId:int}")]
        public async Task<IActionResult> SubmissionFile(int fileId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var file = await _context.SubmissionFiles
                .AsNoTracking()
                .Include(f => f.Submission)
                    .ThenInclude(s => s.Conference)
                .FirstOrDefaultAsync(f => f.Id == fileId);

            if (file == null) return NotFound();

            var submission = file.Submission;
            if (submission == null) return NotFound();

            // Yetki: dosya sahibi, SuperAdmin veya aynı tenant'ın Admin'i
            var isSuperAdmin = User.IsInRole("SuperAdmin");
            var isOwner = submission.AuthorId == user.Id;

            // Admin: DB'deki TenantId ile karşılaştır — slug gerektirmez
            var isTenantAdmin = false;
            if (!isOwner && !isSuperAdmin && User.IsInRole("Admin") && submission.Conference != null)
            {
                var adminTenantId = await _tenantAccess.GetAdminTenantIdAsync(User);
                isTenantAdmin = adminTenantId.HasValue &&
                                adminTenantId.Value == submission.Conference.TenantId;
            }

            var isAdmin = isSuperAdmin || isTenantAdmin;

            var isReviewer = false;
            if (!isOwner && !isAdmin)
            {
                isReviewer = User.IsInRole("Referee") &&
                    await _context.ReviewAssignments
                        .AsNoTracking()
                        .AnyAsync(ra => ra.SubmissionId == submission.Id && ra.ReviewerId == user.Id);
            }

            if (!isOwner && !isAdmin && !isReviewer)
                return Forbid();

            var blindReview = isReviewer && (submission.Conference?.IsBlindReview ?? true);

            await _audit.LogAsync(
                category: "FileDownload",
                action: blindReview ? "SubmissionFileDownloadedAnonymized" : "SubmissionFileDownloaded",
                userId: user.Id,
                userName: $"{user.FirstName} {user.LastName}".Trim(),
                entityType: "SubmissionFile",
                entityId: fileId.ToString(),
                description: $"Bildiri dosyası indirildi{(blindReview ? " (anonim)" : "")}: {file.FileName} (SubmissionId={submission.Id})",
                conferenceId: submission.ConferenceId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            if (blindReview)
                return ServeAnonymized(file.FilePath, submission.Id);

            return ServeFile(file.FilePath, file.FileName ?? "dosya");
        }

        // ── Ödeme Makbuzu ────────────────────────────────────────────────────────

        [HttpGet("/download/receipt/{registrationId:guid}")]
        public async Task<IActionResult> Receipt(Guid registrationId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .AsNoTracking()
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration == null || string.IsNullOrWhiteSpace(registration.ReceiptFilePath))
                return NotFound();

            // Yetki: kayıt sahibi, SuperAdmin veya aynı tenant'ın Admin'i
            var isOwner = registration.AppUserId == user.Id;
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            var isTenantAdmin = false;
            if (!isOwner && !isSuperAdmin && User.IsInRole("Admin") && registration.Conference != null)
            {
                var adminTenantId = await _tenantAccess.GetAdminTenantIdAsync(User);
                isTenantAdmin = adminTenantId.HasValue &&
                                adminTenantId.Value == registration.Conference.TenantId;
            }

            if (!isOwner && !isSuperAdmin && !isTenantAdmin)
                return Forbid();

            await _audit.LogAsync(
                category: "FileDownload",
                action: "ReceiptDownloaded",
                userId: user.Id,
                userName: $"{user.FirstName} {user.LastName}".Trim(),
                entityType: "Registration",
                entityId: registrationId.ToString(),
                description: $"Ödeme makbuzu indirildi. RegistrationId={registrationId}",
                conferenceId: registration.ConferenceId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return ServeFile(registration.ReceiptFilePath, $"makbuz_{registrationId:N}.pdf");
        }

        // ── Yardımcı ────────────────────────────────────────────────────────────

        private IActionResult ServeAnonymized(string relativePath, Guid submissionId)
        {
            string fullPath;
            try
            {
                fullPath = PrivateStorage.Resolve(_env, relativePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "İzin verilmeyen dosya yolu istendi: {Path}", relativePath);
                return BadRequest();
            }

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var anonymousName = $"submission-{submissionId.ToString("N")[..8]}{ext}";

            if (ext == ".pdf")
            {
                try
                {
                    var originalBytes = System.IO.File.ReadAllBytes(fullPath);
                    var code = $"SUB-{submissionId.ToString("N")[..8].ToUpper()}";
                    var anonymizedBytes = _pdfAnonymizer.AnonymizeMetadata(originalBytes, code);
                    return File(anonymizedBytes, "application/pdf", anonymousName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PDF anonimleştirme başarısız, orijinal dosya sunuluyor. FileId={Path}", relativePath);
                }
            }

            return PhysicalFile(fullPath, "application/octet-stream", anonymousName);
        }

        private IActionResult ServeFile(string relativePath, string downloadName)
        {
            string fullPath;
            try
            {
                // PrivateStorage.Resolve: whitelist dışı her yol UnauthorizedAccessException fırlatır
                fullPath = PrivateStorage.Resolve(_env, relativePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "İzin verilmeyen dosya yolu istendi: {Path}", relativePath);
                return BadRequest();
            }

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };

            return PhysicalFile(fullPath, contentType, downloadName);
        }
    }
}
