using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class BroadcastController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IEmailService _emailService;

        public BroadcastController(
            AppDbContext context,
            IAdminTenantAccessService tenantAccess,
            IEmailService emailService)
        {
            _context = context;
            _tenantAccess = tenantAccess;
            _emailService = emailService;
        }

        private async Task<Guid?> GetTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        // ── GET ───────────────────────────────────────────────────────────────────

        [HttpGet("/Admin/Broadcast")]
        [HttpGet("/{slug}/Admin/Broadcast")]
        public async Task<IActionResult> Index(string? slug = null)
        {
            var tenantId = await GetTenantIdAsync();
            if (!tenantId.HasValue) return Challenge();

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId.Value)
                .OrderByDescending(c => c.StartDate)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            ViewBag.Conferences = conferences;
            ViewBag.Slug = slug ?? "";
            return View();
        }

        // ── POST — önizleme (kaç kişi etkilenecek) ──────────────────────────────

        [HttpPost("/Admin/Broadcast/Preview")]
        [HttpPost("/{slug}/Admin/Broadcast/Preview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(
            Guid conferenceId,
            string group,
            string? slug = null)
        {
            var tenantId = await GetTenantIdAsync();
            if (!tenantId.HasValue) return Json(new { count = 0 });

            var emails = await GetRecipientEmailsAsync(conferenceId, group, tenantId.Value);
            return Json(new { count = emails.Count, sample = emails.Take(5) });
        }

        // ── POST — gönder ─────────────────────────────────────────────────────────

        [HttpPost("/Admin/Broadcast/Send")]
        [HttpPost("/{slug}/Admin/Broadcast/Send")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(
            Guid conferenceId,
            string group,
            string subject,
            string body,
            string? slug = null)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            {
                TempData["ErrorMessage"] = "Konu ve içerik boş olamaz.";
                return RedirectToAction(nameof(Index), new { slug });
            }

            var tenantId = await GetTenantIdAsync();
            if (!tenantId.HasValue) return Challenge();

            var emails = await GetRecipientEmailsAsync(conferenceId, group, tenantId.Value);

            int sent = 0, failed = 0;

            foreach (var email in emails)
            {
                try
                {
                    await _emailService.SendAsync(email, subject, body);
                    sent++;
                }
                catch
                {
                    failed++;
                }
            }

            TempData["SuccessMessage"] = failed == 0
                ? $"E-posta başarıyla {sent} kişiye gönderildi."
                : $"{sent} kişiye gönderildi, {failed} başarısız oldu.";

            return RedirectToAction(nameof(Index), new { slug });
        }

        // ── Yardımcı ─────────────────────────────────────────────────────────────

        private async Task<List<string>> GetRecipientEmailsAsync(
            Guid conferenceId, string group, Guid tenantId)
        {
            // Konferans tenant doğrulaması
            var confExists = await _context.Conferences
                .AsNoTracking()
                .AnyAsync(c => c.Id == conferenceId && c.TenantId == tenantId);

            if (!confExists) return new List<string>();

            IQueryable<string?> emailQuery;

            switch (group?.ToLowerInvariant())
            {
                // Kabul edilen yazar bildiri sahipleri
                case "accepted":
                    emailQuery = _context.Submissions
                        .AsNoTracking()
                        .Include(s => s.User)
                        .Where(s => s.ConferenceId == conferenceId &&
                            (s.Status == SubmissionStatus.Accepted ||
                             s.Status == SubmissionStatus.Presented) &&
                            s.User != null)
                        .Select(s => s.User!.Email);
                    break;

                // Hakemler
                case "reviewers":
                    emailQuery = _context.ReviewAssignments
                        .AsNoTracking()
                        .Include(ra => ra.Submission)
                        .Include(ra => ra.Reviewer)
                        .Where(ra => ra.Submission != null &&
                            ra.Submission.ConferenceId == conferenceId &&
                            ra.Reviewer != null)
                        .Select(ra => ra.Reviewer!.Email);
                    break;

                // Kayıt yaptıran (kayıt var) herkes
                case "registered":
                    emailQuery = _context.Registrations
                        .AsNoTracking()
                        .Include(r => r.AppUser)
                        .Where(r => r.ConferenceId == conferenceId && r.AppUser != null)
                        .Select(r => r.AppUser!.Email);
                    break;

                // Ödeme yapanlar
                case "paid":
                    emailQuery = _context.Payments
                        .AsNoTracking()
                        .Include(p => p.AppUser)
                        .Where(p => p.ConferenceId == conferenceId &&
                            p.Status == PaymentStatus.Completed &&
                            p.AppUser != null)
                        .Select(p => p.AppUser!.Email);
                    break;

                // Tüm bildirim sahipleri (varsayılan)
                default:
                    emailQuery = _context.Submissions
                        .AsNoTracking()
                        .Include(s => s.User)
                        .Where(s => s.ConferenceId == conferenceId && s.User != null)
                        .Select(s => s.User!.Email);
                    break;
            }

            return await emailQuery
                .Where(e => e != null && e.Contains("@"))
                .Distinct()
                .Cast<string>()
                .ToListAsync();
        }
    }
}
