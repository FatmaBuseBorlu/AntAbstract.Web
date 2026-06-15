using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    public class EmailTemplatesController : Controller
    {
        private readonly AppDbContext _context;

        public EmailTemplatesController(AppDbContext context)
        {
            _context = context;
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
