using AntAbstract.Application.Interfaces;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;

namespace AntAbstract.Infrastructure.Services
{
    /// <summary>
    /// Toplu duyuru yalnızca e-posta gönderiyordu; spam klasörüne düşen ya da
    /// e-postasına bakmayan katılımcı duyurudan habersiz kalıyordu. Aynı alıcılara
    /// sistem içi bildirim de düşer (hemen gönderim ve zamanlanmış gönderim).
    /// </summary>
    public static class BroadcastInAppNotifier
    {
        public static async Task<int> NotifyAsync(
            AppDbContext context,
            INotificationService notifications,
            Guid conferenceId,
            IReadOnlyCollection<string> emails,
            string subject,
            string htmlBody,
            CancellationToken ct = default)
        {
            if (emails.Count == 0)
                return 0;

            var conference = await context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId, ct);

            var normalized = emails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            var userIds = await context.Users
                .AsNoTracking()
                .Where(u => u.NormalizedEmail != null && normalized.Contains(u.NormalizedEmail))
                .Select(u => u.Id)
                .ToListAsync(ct);

            var slug = string.IsNullOrWhiteSpace(conference?.Slug) ? conference?.Tenant?.Slug : conference.Slug;
            var link = string.IsNullOrWhiteSpace(slug) ? null : $"/{slug}";
            var message = Snippet(htmlBody, conference?.Title);

            foreach (var userId in userIds)
            {
                await notifications.CreateAsync(
                    userId: userId,
                    title: subject,
                    message: message,
                    icon: "📢",
                    color: "primary",
                    link: link);
            }

            return userIds.Count;
        }

        // Bildirim kutusunda HTML gösterilmez: etiketleri atıp kısaltır.
        private static string Snippet(string html, string? conferenceTitle)
        {
            var text = WebUtility.HtmlDecode(Regex.Replace(html ?? "", "<[^>]+>", " "));
            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (text.Length > 180)
                text = text[..180].TrimEnd() + "…";

            return string.IsNullOrWhiteSpace(conferenceTitle) ? text : $"{conferenceTitle}: {text}";
        }
    }
}
