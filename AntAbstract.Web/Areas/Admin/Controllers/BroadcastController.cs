using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly IEmailQueue _emailQueue;
        private readonly UserManager<AppUser> _userManager;

        public BroadcastController(
            AppDbContext context,
            IAdminTenantAccessService tenantAccess,
            IEmailQueue emailQueue,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantAccess = tenantAccess;
            _emailQueue = emailQueue;
            _userManager = userManager;
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

            var scheduled = await _context.ScheduledBroadcasts
                .AsNoTracking()
                .Include(b => b.Conference)
                .Include(b => b.CreatedByUser)
                .Where(b => b.TenantId == tenantId.Value)
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.Conferences = conferences;
            ViewBag.Slug = slug ?? "";
            ViewBag.ScheduledBroadcasts = scheduled;
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
            DateTime? scheduledAt,
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
            var user = await _userManager.GetUserAsync(User);

            if (scheduledAt.HasValue && scheduledAt.Value > DateTime.UtcNow.AddMinutes(5))
            {
                _context.ScheduledBroadcasts.Add(new ScheduledBroadcast
                {
                    ConferenceId = conferenceId,
                    TargetGroup = group,
                    Subject = subject,
                    HtmlBody = body,
                    ScheduledAt = scheduledAt.Value,
                    RecipientCount = emails.Count,
                    Status = BroadcastStatus.Pending,
                    CreatedByUserId = user?.Id,
                    TenantId = tenantId.Value,
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{emails.Count} kişiye {scheduledAt.Value:dd.MM.yyyy HH:mm} tarihinde gönderilecek.";
            }
            else
            {
                foreach (var email in emails)
                {
                    _emailQueue.Enqueue(new EmailQueueItem(email, subject, body));
                }

                _context.ScheduledBroadcasts.Add(new ScheduledBroadcast
                {
                    ConferenceId = conferenceId,
                    TargetGroup = group,
                    Subject = subject,
                    HtmlBody = body,
                    ScheduledAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    RecipientCount = emails.Count,
                    Status = BroadcastStatus.Sent,
                    CreatedByUserId = user?.Id,
                    TenantId = tenantId.Value,
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{emails.Count} kişi için e-posta kuyruğa alındı. Arka planda gönderilecek.";
            }

            return RedirectToAction(nameof(Index), new { slug });
        }

        [HttpPost("/Admin/Broadcast/Cancel")]
        [HttpPost("/{slug}/Admin/Broadcast/Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int broadcastId, string? slug = null)
        {
            var tenantId = await GetTenantIdAsync();
            if (!tenantId.HasValue) return Challenge();

            var broadcast = await _context.ScheduledBroadcasts
                .FirstOrDefaultAsync(b => b.Id == broadcastId && b.TenantId == tenantId.Value);

            if (broadcast == null || broadcast.Status != BroadcastStatus.Pending)
            {
                TempData["ErrorMessage"] = "Zamanlanmış gönderim bulunamadı veya zaten gönderilmiş.";
                return RedirectToAction(nameof(Index), new { slug });
            }

            broadcast.Status = BroadcastStatus.Cancelled;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Zamanlanmış gönderim iptal edildi.";
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

                // Revizyon gerekli bildiri sahipleri
                case "revision":
                    emailQuery = _context.Submissions
                        .AsNoTracking()
                        .Include(s => s.User)
                        .Where(s => s.ConferenceId == conferenceId &&
                            s.Status == SubmissionStatus.RevisionRequired &&
                            s.User != null)
                        .Select(s => s.User!.Email);
                    break;

                // Kayıt yaptırıp ödeme yapmamışlar
                case "unpaid":
                    emailQuery = _context.Registrations
                        .AsNoTracking()
                        .Include(r => r.AppUser)
                        .Where(r => r.ConferenceId == conferenceId &&
                            !r.IsPaid &&
                            r.Status != RegistrationStatus.Cancelled &&
                            r.AppUser != null)
                        .Select(r => r.AppUser!.Email);
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
