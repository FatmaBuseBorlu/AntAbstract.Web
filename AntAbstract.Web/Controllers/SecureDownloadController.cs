using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
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
    /// wwwroot/uploads/* doğrudan erişim Program.cs'te engellenmiştir.
    /// </summary>
    [Authorize]
    public class SecureDownloadController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public SecureDownloadController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
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

            // Yetki: ya dosyanın sahibidir ya da admin/hakem
            var isOwner = submission.AuthorId == user.Id;
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            var isReviewer = User.IsInRole("Referee") &&
                await _context.ReviewAssignments
                    .AsNoTracking()
                    .AnyAsync(ra => ra.SubmissionId == submission.Id && ra.ReviewerId == user.Id);

            if (!isOwner && !isAdmin && !isReviewer)
                return Forbid();

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

            // Yetki: kayıt sahibi veya admin
            var isOwner = registration.AppUserId == user.Id;
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

            if (!isOwner && !isAdmin)
                return Forbid();

            var fileName = Path.GetFileName(registration.ReceiptFilePath);
            return ServeFile(registration.ReceiptFilePath, $"makbuz_{registrationId:N}.pdf");
        }

        // ── Yardımcı ────────────────────────────────────────────────────────────

        private IActionResult ServeFile(string relativePath, string downloadName)
        {
            // /uploads/... → wwwroot/uploads/...
            var fullPath = Path.Combine(_env.WebRootPath,
                relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            // Path traversal koruması: dosya wwwroot altında olmalı
            var wwwroot = Path.GetFullPath(_env.WebRootPath);
            var resolved = Path.GetFullPath(fullPath);
            if (!resolved.StartsWith(wwwroot, StringComparison.OrdinalIgnoreCase))
                return BadRequest();

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf"  => "application/pdf",
                ".doc"  => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"  => "image/png",
                _       => "application/octet-stream"
            };

            return PhysicalFile(resolved, contentType, downloadName);
        }
    }
}
