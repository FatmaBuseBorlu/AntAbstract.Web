using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    public class EmailTemplatesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly UserManager<AppUser> _userManager;

        public EmailTemplatesController(
            AppDbContext context,
            IEmailService emailService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _emailService = emailService;
            _userManager = userManager;
        }

        // GET /Admin/EmailTemplates
        public async Task<IActionResult> Index()
        {
            var templates = await _context.EmailTemplates
                .OrderBy(t => t.Key)
                .ToListAsync();

            // İlk açılışta varsayılan şablonları seed et
            if (!templates.Any())
            {
                await SeedDefaultTemplatesAsync();
                templates = await _context.EmailTemplates.OrderBy(t => t.Key).ToListAsync();
            }

            return View(templates);
        }

        // GET /Admin/EmailTemplates/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var template = await _context.EmailTemplates.FindAsync(id);
            if (template == null) return NotFound();
            return View(template);
        }

        // POST /Admin/EmailTemplates/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmailTemplate model)
        {
            if (id != model.Id) return BadRequest();

            if (!ModelState.IsValid) return View(model);

            var template = await _context.EmailTemplates.FindAsync(id);
            if (template == null) return NotFound();

            template.Description = model.Description;
            template.Subject     = model.Subject;
            template.HtmlBody    = model.HtmlBody;
            template.IsActive    = model.IsActive;
            template.UpdatedAt   = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "E-posta şablonu başarıyla güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/EmailTemplates/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var template = await _context.EmailTemplates.FindAsync(id);
            if (template == null) return NotFound();

            template.IsActive  = !template.IsActive;
            template.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/EmailTemplates/Preview/5
        public async Task<IActionResult> Preview(int id)
        {
            var template = await _context.EmailTemplates.FindAsync(id);
            if (template == null) return NotFound();

            // Placeholder'ları örnek verilerle doldur
            var previewBody = template.HtmlBody
                .Replace("{FullName}", "Örnek Kullanıcı")
                .Replace("{ConferenceTitle}", "Örnek Kongre 2026")
                .Replace("{SubmissionTitle}", "Örnek Bildiri Başlığı")
                .Replace("{Note}", "<p><strong>Not:</strong> Hakem değerlendirme notu burada yer alır.</p>")
                .Replace("{DownloadLink}", "#")
                .Replace("{Amount}", "500,00 TRY")
                .Replace("{TransactionId}", "TXN-20260616");

            return Content(previewBody, "text/html");
        }

        // POST /Admin/EmailTemplates/TestSend/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestSend(int id)
        {
            var template = await _context.EmailTemplates.FindAsync(id);
            if (template == null) return NotFound();

            var adminUser = await _userManager.GetUserAsync(User);
            if (adminUser?.Email == null)
            {
                TempData["Error"] = "Kullanıcı e-postası bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var subject = "[TEST] " + template.Subject
                .Replace("{ConferenceTitle}", "Test Kongre")
                .Replace("{SubmissionTitle}", "Test Bildiri");

            var body = template.HtmlBody
                .Replace("{FullName}", $"{adminUser.FirstName} {adminUser.LastName}".Trim())
                .Replace("{ConferenceTitle}", "Test Kongre 2026")
                .Replace("{SubmissionTitle}", "Test Bildiri Başlığı")
                .Replace("{Note}", "<p><strong>Not:</strong> Bu bir test gönderisidir.</p>")
                .Replace("{DownloadLink}", "#")
                .Replace("{Amount}", "500,00 TRY")
                .Replace("{TransactionId}", "TXN-TEST-001");

            try
            {
                await _emailService.SendAsync(adminUser.Email, subject, body);
                TempData["Success"] = $"Test e-postası {adminUser.Email} adresine gönderildi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"E-posta gönderilemedi: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/EmailTemplates/SendLog
        public async Task<IActionResult> SendLog(
            string? email = null,
            string? status = null,
            string? templateKey = null,
            int page = 1)
        {
            const int pageSize = 50;

            var query = _context.EmailLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(l => l.ToEmail.Contains(email));
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(l => l.Status == status);
            if (!string.IsNullOrWhiteSpace(templateKey))
                query = query.Where(l => l.TemplateKey == templateKey);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.FilterEmail = email;
            ViewBag.FilterStatus = status;
            ViewBag.FilterTemplateKey = templateKey;

            // Şablon key listesi filtre dropdown için
            ViewBag.TemplateKeys = await _context.EmailTemplates
                .AsNoTracking()
                .Select(t => t.Key)
                .OrderBy(k => k)
                .ToListAsync();

            return View(items);
        }

        // -----------------------------------------------------------------
        // Varsayılan şablonları DB'ye ekler (sadece ilk açılışta)
        // -----------------------------------------------------------------
        private async Task SeedDefaultTemplatesAsync()
        {
            var defaults = new List<EmailTemplate>
            {
                new EmailTemplate
                {
                    Key         = "decision.accept",
                    Description = "Bildiri kabul edildiğinde yazara gönderilir.",
                    Subject     = "Bildiriniz Kabul Edildi – {ConferenceTitle}",
                    HtmlBody    = @"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:#16a34a;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>✅ Bildiriniz Kabul Edildi</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
    <p>Sayın <strong>{FullName}</strong>,</p>
    <p><strong>{ConferenceTitle}</strong> kongresine gönderdiğiniz <em>{SubmissionTitle}</em> başlıklı bildiri kabul edilmiştir.</p>
    {Note}
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>",
                    IsActive    = true
                },
                new EmailTemplate
                {
                    Key         = "decision.reject",
                    Description = "Bildiri reddedildiğinde yazara gönderilir.",
                    Subject     = "Bildiri Değerlendirme Sonucu – {ConferenceTitle}",
                    HtmlBody    = @"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:#dc2626;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>❌ Bildiri Değerlendirme Sonucu</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
    <p>Sayın <strong>{FullName}</strong>,</p>
    <p><strong>{ConferenceTitle}</strong> kongresine gönderdiğiniz <em>{SubmissionTitle}</em> başlıklı bildiri maalesef kabul edilememiştir.</p>
    {Note}
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>",
                    IsActive    = true
                },
                new EmailTemplate
                {
                    Key         = "decision.revision",
                    Description = "Bildiri revizyona gönderildiğinde yazara gönderilir.",
                    Subject     = "Bildiriniz Revizyon Gerektiriyor – {ConferenceTitle}",
                    HtmlBody    = @"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:#d97706;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>🔄 Bildiriniz Revizyon Gerektiriyor</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
    <p>Sayın <strong>{FullName}</strong>,</p>
    <p><strong>{ConferenceTitle}</strong> kongresine gönderdiğiniz <em>{SubmissionTitle}</em> başlıklı bildirinizin revize edilmesi gerekmektedir.</p>
    {Note}
    <p>Lütfen sisteme giriş yaparak bildirinizi güncelleyiniz.</p>
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>",
                    IsActive    = true
                },
                new EmailTemplate
                {
                    Key         = "certificate.author",
                    Description = "Yazar sertifikası oluşturulduğunda gönderilir.",
                    Subject     = "Katılım Sertifikanız Hazır – {ConferenceTitle}",
                    HtmlBody    = @"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:#7c3aed;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>🏆 Sertifikanız Hazır</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
    <p>Sayın <strong>{FullName}</strong>,</p>
    <p><strong>{ConferenceTitle}</strong> kongresine yazar olarak katılımınıza ait sertifikanız hazırlanmıştır.</p>
    <p><a href='{DownloadLink}' style='background:#7c3aed;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;margin-top:12px'>Sertifikayı İndir</a></p>
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>",
                    IsActive    = true
                },
                new EmailTemplate
                {
                    Key         = "certificate.reviewer",
                    Description = "Hakem sertifikası oluşturulduğunda gönderilir.",
                    Subject     = "Hakem Sertifikanız Hazır – {ConferenceTitle}",
                    HtmlBody    = @"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:#0284c7;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>🏅 Hakem Sertifikanız Hazır</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
    <p>Sayın <strong>{FullName}</strong>,</p>
    <p><strong>{ConferenceTitle}</strong> kongresinde hakem olarak görev yaptığınız için teşekkür ederiz. Sertifikanız hazırlanmıştır.</p>
    <p><a href='{DownloadLink}' style='background:#0284c7;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;margin-top:12px'>Sertifikayı İndir</a></p>
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>",
                    IsActive    = true
                },
                new EmailTemplate
                {
                    Key         = "certificate.attendee",
                    Description = "Dinleyici katılım sertifikası oluşturulduğunda gönderilir.",
                    Subject     = "Katılım Sertifikanız Hazır – {ConferenceTitle}",
                    HtmlBody    = @"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
  <div style='background:#0891b2;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
    <h2 style='margin:0'>📜 Katılım Sertifikanız Hazır</h2>
  </div>
  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
    <p>Sayın <strong>{FullName}</strong>,</p>
    <p><strong>{ConferenceTitle}</strong> kongresine dinleyici olarak katılımınıza ait sertifikanız hazırlanmıştır.</p>
    <p><a href='{DownloadLink}' style='background:#0891b2;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;margin-top:12px'>Sertifikayı İndir</a></p>
    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
  </div>
</div>",
                    IsActive    = true
                }
            };

            _context.EmailTemplates.AddRange(defaults);
            await _context.SaveChangesAsync();
        }
    }
}
