using System.Collections.Concurrent;

namespace AntAbstract.Web.Infrastructure
{
    public static class RateLimitCounter
    {
        private static readonly ConcurrentQueue<RateLimitRejection> _rejections = new();
        private const int MaxEntries = 100;

        public static void Record(string path, string? ip)
        {
            _rejections.Enqueue(new RateLimitRejection(DateTime.UtcNow, path, ip));
            while (_rejections.Count > MaxEntries)
                _rejections.TryDequeue(out _);
        }

        public static List<RateLimitRejection> GetRecentRejections(int count = 20)
        {
            return _rejections
                .OrderByDescending(r => r.At)
                .Take(count)
                .ToList();
        }

        public record RateLimitRejection(DateTime At, string Path, string? Ip);
    }
}
