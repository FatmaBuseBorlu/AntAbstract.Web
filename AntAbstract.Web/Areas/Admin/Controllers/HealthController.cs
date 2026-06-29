using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Infrastructure;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    public class HealthController : Controller
    {
        private readonly AppDbContext _context;
        private readonly AuditQueue _auditQueue;
        private readonly IConfiguration _configuration;

        public HealthController(
            AppDbContext context,
            AuditQueue auditQueue,
            IConfiguration configuration)
        {
            _context = context;
            _auditQueue = auditQueue;
            _configuration = configuration;
        }

        [HttpGet("/Admin/Health")]
        [HttpGet("/{slug}/Admin/Health")]
        public async Task<IActionResult> Index(string? slug = null)
        {
            // ── DB bağlantısı ────────────────────────────────────────────────
            var dbOk = false;
            string? dbError = null;
            try
            {
                dbOk = await _context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                dbError = ex.Message;
            }

            // ── Stripe yapılandırması ────────────────────────────────────────
            var stripeKey = _configuration["Stripe:SecretKey"];
            var stripeOk = HasConfiguredValue(stripeKey);

            // ── Audit queue ──────────────────────────────────────────────────
            var auditQueuePending = _auditQueue.PendingCount;

            // ── Son webhook hataları (son 20) ────────────────────────────────
            var recentWebhookErrors = await _context.StripeWebhookEvents
                .AsNoTracking()
                .Where(e => e.Status == "failed")
                .OrderByDescending(e => e.ReceivedAt)
                .Take(20)
                .Select(e => new WebhookErrorRow(
                    e.ReceivedAt,
                    e.EventType,
                    e.StripeEventId,
                    e.ErrorMessage))
                .ToListAsync();

            // ── Son mail hataları (son 20) ────────────────────────────────────
            var recentMailErrors = await _context.EmailLogs
                .AsNoTracking()
                .Where(e => e.Status == "failed")
                .OrderByDescending(e => e.SentAt)
                .Take(20)
                .Select(e => new MailErrorRow(
                    e.SentAt,
                    e.ToEmail,
                    e.Subject,
                    e.ErrorMessage,
                    e.TemplateKey))
                .ToListAsync();

            // ── Son 24 saatteki istatistikler ────────────────────────────────
            var since24h = DateTime.UtcNow.AddHours(-24);

            var webhookStats = new WebhookStats(
                Total: await _context.StripeWebhookEvents.CountAsync(e => e.ReceivedAt >= since24h),
                Failed: await _context.StripeWebhookEvents.CountAsync(e => e.ReceivedAt >= since24h && e.Status == "failed"),
                Duplicate: await _context.StripeWebhookEvents.CountAsync(e => e.ReceivedAt >= since24h && e.IsDuplicate));

            var mailStats = new MailStats(
                Total: await _context.EmailLogs.CountAsync(e => e.SentAt >= since24h),
                Failed: await _context.EmailLogs.CountAsync(e => e.SentAt >= since24h && e.Status == "failed"));

            // ── Hatırlatıcı şablon kontrolü ──────────────────────────────────
            var reminderTemplateKeys = new[] { "deadline_reminder", "payment_pending_reminder" };
            var existingKeys = await _context.EmailTemplates
                .AsNoTracking()
                .Where(t => reminderTemplateKeys.Contains(t.Key) && t.IsActive)
                .Select(t => t.Key)
                .ToListAsync();
            var missingReminderTemplates = reminderTemplateKeys
                .Where(k => !existingKeys.Contains(k))
                .ToList();

            var rateLimitPolicies = new[]
            {
                new { Policy = "auth", Limit = "10 istek / 5 dk", Scope = "Login, Register, ForgotPassword" },
                new { Policy = "upload", Limit = "20 istek / dk", Scope = "Dosya yükleme" },
                new { Policy = "webhook", Limit = "60 istek / dk", Scope = "Stripe/PayTR callback" },
                new { Policy = "api", Limit = "30 istek / dk", Scope = "REST API endpoint'leri" },
                new { Policy = "api-auth", Limit = "5 istek / 5 dk", Scope = "API login (brute force koruması)" }
            };

            var rateLimitRejections = RateLimitCounter.GetRecentRejections();

            ViewBag.DbOk = dbOk;
            ViewBag.DbError = dbError;
            ViewBag.StripeOk = stripeOk;
            ViewBag.AuditQueuePending = auditQueuePending;
            ViewBag.RecentWebhookErrors = recentWebhookErrors;
            ViewBag.RecentMailErrors = recentMailErrors;
            ViewBag.WebhookStats = webhookStats;
            ViewBag.MailStats = mailStats;
            ViewBag.MissingReminderTemplates = missingReminderTemplates;
            ViewBag.RateLimitPolicies = rateLimitPolicies;
            ViewBag.RateLimitRejections = rateLimitRejections;
            ViewBag.Slug = slug;
            ViewBag.GeneratedAt = DateTime.UtcNow;

            return View();
        }

        // ── JSON endpoint: uptime monitoring araçları için ───────────────────
        // Erişim: oturum açmış Admin VEYA X-Health-Key header ile API key doğrulaması.
        // appsettings.json → Health:StatusApiKey (ortam değişkeni ile override edilmeli).

        [AllowAnonymous]
        [HttpGet("/Admin/Health/Status")]
        [HttpGet("/{slug}/Admin/Health/Status")]
        public async Task<IActionResult> Status()
        {
            if (!IsStatusAuthorized())
                return Unauthorized(new { error = "Geçersiz veya eksik API anahtarı." });

            var dbOk = false;
            try { dbOk = await _context.Database.CanConnectAsync(); } catch { }

            var stripeKey = _configuration["Stripe:SecretKey"];
            var stripeOk = HasConfiguredValue(stripeKey);

            return Ok(new
            {
                status = dbOk ? "healthy" : "degraded",
                timestamp = DateTime.UtcNow,
                checks = new
                {
                    database = dbOk ? "ok" : "error",
                    stripe = stripeOk ? "ok" : "not_configured",
                    auditQueuePending = _auditQueue.PendingCount
                }
            });
        }

        private bool IsStatusAuthorized()
        {
            // Oturum açmış admin her zaman erişebilir
            if (User.Identity?.IsAuthenticated == true &&
                (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
                return true;

            // Dış araçlar için X-Health-Key header kontrolü
            var configuredKey = _configuration["Health:StatusApiKey"];
            if (!HasConfiguredValue(configuredKey))
                return false;

            var providedKey = Request.Headers["X-Health-Key"].FirstOrDefault();
            return string.Equals(configuredKey!.Trim(), providedKey, StringComparison.Ordinal);
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

    public record WebhookErrorRow(DateTime ReceivedAt, string EventType, string StripeEventId, string? ErrorMessage);
    public record MailErrorRow(DateTime SentAt, string ToEmail, string Subject, string? ErrorMessage, string? TemplateKey);
    public record WebhookStats(int Total, int Failed, int Duplicate);
    public record MailStats(int Total, int Failed);
}
