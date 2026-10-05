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
            // Eksik varsayılan şablonları ekler (düzenlenmişlere dokunmaz).
            // Açılışta da çalışır; burada tekrar etmek yeni kurulumda ekranın
            // boş gelmemesi için.
            await AntAbstract.Infrastructure.Services.Email.EmailTemplateDefaults.EnsureAsync(_context);

            var templates = await _context.EmailTemplates
                .OrderBy(t => t.Key)
                .ToListAsync();

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
            template.Subject = model.Subject;
            template.HtmlBody = model.HtmlBody;
            template.IsActive = model.IsActive;
            template.UpdatedAt = DateTime.UtcNow;

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

            template.IsActive = !template.IsActive;
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
    }
}
