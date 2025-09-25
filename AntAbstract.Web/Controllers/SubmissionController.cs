using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // Sadece giriş yapan kullanıcılar erişebilir
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public SubmissionController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: Submission/Index
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var submissions = await _context.Submissions
                .Where(s => s.AuthorId == userId)
                .ToListAsync();
            return View(submissions);
        }

        // GET: Submission/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Submission/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Submission submission, IFormFile submissionFile)
        {
            if (ModelState.IsValid)
            {
                // 1. KONGRE (TENANT) KONTROLÜ
                if (_tenantContext.Current == null)
                {
                    ModelState.AddModelError("", "Aktif bir kongre URL'i (/slug) üzerinden işlem yapmalısınız.");
                    return View(submission);
                }
                var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
                if (conference == null)
                {
                    ModelState.AddModelError("", "Bu kongre için bir konferans kaydı bulunamadı.");
                    return View(submission);
                }

                // 2. DOSYA YÜKLEME KONTROLÜ
                if (submissionFile == null || submissionFile.Length == 0)
                {
                    ModelState.AddModelError("FilePath", "Lütfen bir özet dosyası seçin.");
                    return View(submission);
                }

                // Dosyayı sunucuya kaydet
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + submissionFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await submissionFile.CopyToAsync(fileStream);
                }

                // 3. SUBMISSION NESNESİNİ DOLDURMA
                submission.FilePath = "/uploads/" + uniqueFileName; // Veritabanına kaydedilecek yol
                submission.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                submission.ConferenceId = conference.Id;
                submission.CreatedAt = DateTime.UtcNow;

                // 4. VERİTABANINA KAYDETME
                _context.Add(submission);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            // ModelState geçerli değilse, formu hatalarla geri göster.
            return View(submission);
        }
    }
}