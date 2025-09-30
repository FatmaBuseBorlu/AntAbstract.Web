using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IEmailService _emailService;
        private readonly UserManager<AppUser> _userManager;

        public SubmissionController(AppDbContext context, TenantContext tenantContext, IEmailService emailService, UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var submissions = await _context.Submissions
                .Where(s => s.AuthorId == userId)
                .ToListAsync();
            return View(submissions);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,AbstractText,Keywords")] Submission submission, IFormFile submissionFile)
        {
            if (_tenantContext.Current == null)
            {
                ModelState.AddModelError("", "Aktif bir kongre URL'i üzerinden işlem yapmalısınız.");
                return View(submission);
            }
            var conference = await _context.Conferences.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
            if (conference == null)
            {
                ModelState.AddModelError("", "Bu kongre için bir konferans kaydı bulunamadı.");
                return View(submission);
            }

            if (submissionFile == null || submissionFile.Length == 0)
            {
                ModelState.AddModelError("", "Lütfen bir özet dosyası seçin.");
                return View(submission);
            }

            // ✅ EKLENDİ: AuthorId zorunlu alan hatasını engellemek için.
            ModelState.Remove(nameof(submission.AuthorId));

            if (ModelState.IsValid)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(submissionFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await submissionFile.CopyToAsync(fileStream);
                }

                submission.SubmissionId = Guid.NewGuid();
                submission.FilePath = "/uploads/" + uniqueFileName;
                submission.AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                submission.ConferenceId = conference.Id;
                submission.CreatedAt = DateTime.UtcNow;
                submission.Status = "Yeni Gönderildi";

                _context.Add(submission);
                await _context.SaveChangesAsync();

                try
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        await _emailService.SendEmailAsync(user.Email, "Özetiniz Başarıyla Alındı", $"Merhaba {user.DisplayName},<br>'{submission.Title}' başlıklı özetiniz sistemimize başarıyla kaydedilmiştir.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"E-POSTA GÖNDERİM HATASI: {ex.Message}");
                }

                TempData["SuccessMessage"] = "Özetiniz başarıyla gönderildi.";
                return RedirectToAction(nameof(Index));
            }

            return View(submission);
        }

        public async Task<IActionResult> Details(Guid? submissionId)
        {
            if (submissionId == null) return NotFound();
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .FirstOrDefaultAsync(m => m.SubmissionId == submissionId);
            if (submission == null) return NotFound();
            return View(submission);
        }

        public async Task<IActionResult> Edit(Guid? submissionId)
        {
            if (submissionId == null) return NotFound();
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null) return NotFound();
            return View(submission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid submissionId, [Bind("SubmissionId,Title,AbstractText,Keywords")] Submission submission, IFormFile? newSubmissionFile)
        {
            if (submissionId != submission.SubmissionId) return NotFound();

            var submissionToUpdate = await _context.Submissions.FindAsync(submissionId);
            if (submissionToUpdate == null) return NotFound();

            // ✅ EKLENDİ: AuthorId zorunlu alan hatasını engellemek için.
            ModelState.Remove(nameof(submission.AuthorId));

            if (ModelState.IsValid)
            {
                submissionToUpdate.Title = submission.Title;
                submissionToUpdate.AbstractText = submission.AbstractText;
                submissionToUpdate.Keywords = submission.Keywords;

                if (newSubmissionFile != null && newSubmissionFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(submissionToUpdate.FilePath))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", submissionToUpdate.FilePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath)) System.IO.File.Delete(oldFilePath);
                    }
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(newSubmissionFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    await using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await newSubmissionFile.CopyToAsync(fileStream);
                    }
                    submissionToUpdate.FilePath = "/uploads/" + uniqueFileName;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Özetiniz başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            return View(submission);
        }

        // Diğer metotlar (Delete, UploadRevision vb.) burada yer alabilir...
    }
}