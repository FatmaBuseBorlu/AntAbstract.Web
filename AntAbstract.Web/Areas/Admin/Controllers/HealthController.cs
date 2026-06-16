using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
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
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
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
            var stripeOk = !string.IsNullOrWhiteSpace(stripeKey) && !stripeKey.StartsWith("SET_VIA");

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

            ViewBag.DbOk = dbOk;
            ViewBag.DbError = dbError;
            ViewBag.StripeOk = stripeOk;
            ViewBag.AuditQueuePending = auditQueuePending;
            ViewBag.RecentWebhookErrors = recentWebhookErrors;
            ViewBag.RecentMailErrors = recentMailErrors;
            ViewBag.WebhookStats = webhookStats;
            ViewBag.MailStats = mailStats;
            ViewBag.Slug = slug;
            ViewBag.GeneratedAt = DateTime.UtcNow;

            return View();
        }

        // ── JSON endpoint: uptime monitoring araçları için ───────────────────

        [HttpGet("/Admin/Health/Status")]
        [HttpGet("/{slug}/Admin/Health/Status")]
        public async Task<IActionResult> Status()
        {
            var dbOk = false;
            try { dbOk = await _context.Database.CanConnectAsync(); } catch { }

            var stripeKey = _configuration["Stripe:SecretKey"];
            var stripeOk = !string.IsNullOrWhiteSpace(stripeKey) && !stripeKey.StartsWith("SET_VIA");

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
    }

    public record WebhookErrorRow(DateTime ReceivedAt, string EventType, string StripeEventId, string? ErrorMessage);
    public record MailErrorRow(DateTime SentAt, string ToEmail, string Subject, string? ErrorMessage, string? TemplateKey);
    public record WebhookStats(int Total, int Failed, int Duplicate);
    public record MailStats(int Total, int Failed);
}
