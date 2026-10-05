using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security;
using System.Text;

namespace AntAbstract.Web.Controllers
{
    /// <summary>
    /// Arama motorları için robots.txt ve sitemap.xml. Kongre sayfaları
    /// herkese açık olduğundan tüm kongreler (/{slug}) haritaya girer;
    /// panel, hesap ve API adresleri taranmaz.
    /// </summary>
    public class SeoController : Controller
    {
        private static readonly string[] StaticPaths =
        {
            "/", "/congresses", "/proceedings", "/about", "/contact",
            "/privacy", "/kvkk", "/cookies", "/terms"
        };

        private readonly AppDbContext _context;

        public SeoController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("/robots.txt")]
        [ResponseCache(Duration = 86400)]
        public IActionResult Robots()
        {
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Disallow: /Admin/");
            sb.AppendLine("Disallow: /Dashboard/");
            sb.AppendLine("Disallow: /Identity/");
            sb.AppendLine("Disallow: /api/");
            sb.AppendLine("Disallow: /hubs/");
            sb.AppendLine("Disallow: /login");
            sb.AppendLine("Disallow: /*/Admin/");
            sb.AppendLine("Disallow: /*/Dashboard/");
            sb.AppendLine();
            sb.AppendLine($"Sitemap: {BaseUrl()}/sitemap.xml");

            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }

        [HttpGet("/sitemap.xml")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> Sitemap()
        {
            var conferences = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Slug != null && c.Slug != "")
                .OrderByDescending(c => c.StartDate)
                .Select(c => new { c.Slug, c.EndDate })
                .ToListAsync();

            var baseUrl = BaseUrl();
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            foreach (var path in StaticPaths)
                sb.AppendLine($"  <url><loc>{SecurityElement.Escape(baseUrl + path)}</loc></url>");

            foreach (var c in conferences)
            {
                // Bitmiş kongreler seyrek değişir; arama motoruna öyle bildirilir.
                var freq = c.EndDate < DateTime.Now ? "yearly" : "weekly";
                var loc = SecurityElement.Escape($"{baseUrl}/{Uri.EscapeDataString(c.Slug)}");
                sb.AppendLine($"  <url><loc>{loc}</loc><changefreq>{freq}</changefreq></url>");
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml", Encoding.UTF8);
        }

        private string BaseUrl() =>
            $"{Request.Scheme}://{Request.Host.ToString().ToLower(CultureInfo.InvariantCulture)}";
    }
}
