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
    [Authorize]
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
        public async Task<IActionResult> Create(IFormCollection collection, IFormFile submissionFile)
        {
            try
            {
                if (_tenantContext.Current == null)
                {
                    ModelState.AddModelError("", "Aktif bir kongre URL'i (/slug) üzerinden işlem yapmalısınız.");
                    return View();
                }
                var conference = await _context.Conferences.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
                if (conference == null)
                {
                    ModelState.AddModelError("", "Bu kongre için bir konferans kaydı bulunamadı.");
                    return View();
                }

                if (submissionFile == null || submissionFile.Length == 0)
                {
                    ModelState.AddModelError("", "Lütfen bir özet dosyası seçin.");
                    return View();
                }

                string title = collection["Title"];
                if (string.IsNullOrEmpty(title))
                {
                    ModelState.AddModelError("Title", "Başlık alanı zorunludur.");
                    return View();
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetExtension(submissionFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await submissionFile.CopyToAsync(fileStream);
                }

                var newSubmission = new Submission
                {
                    Title = title,
                    AbstractText = collection["AbstractText"],
                    FilePath = "/uploads/" + uniqueFileName,
                    AuthorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    ConferenceId = conference.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Add(newSubmission);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Özetiniz başarıyla gönderildi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ÖZET GÖNDERME HATASI: " + ex.ToString());
                ModelState.AddModelError("", "Beklenmedik bir hata oluştu. Lütfen tekrar deneyin.");
                return View();
            }
        }

        // --- ✅ YENİ EKLENEN DETAY, DÜZENLEME VE SİLME METOTLARI ---

        // GET: Submission/Details/5
        public async Task<IActionResult> Details(Guid? submissionId)
        {
            if (submissionId == null) return NotFound();
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .FirstOrDefaultAsync(m => m.SubmissionId == submissionId);
            if (submission == null) return NotFound();
            return View(submission);
        }

        // GET: Submission/Edit/5
        public async Task<IActionResult> Edit(Guid? submissionId)
        {
            if (submissionId == null) return NotFound();
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null) return NotFound();
            return View(submission);
        }

        // POST: Submission/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid submissionId, IFormCollection collection, IFormFile? newSubmissionFile)
        {
            var submissionToUpdate = await _context.Submissions.FindAsync(submissionId);
            if (submissionToUpdate == null) return NotFound();

            submissionToUpdate.Title = collection["Title"];
            submissionToUpdate.AbstractText = collection["AbstractText"];

            if (newSubmissionFile != null && newSubmissionFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(submissionToUpdate.FilePath))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", submissionToUpdate.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetExtension(newSubmissionFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await newSubmissionFile.CopyToAsync(fileStream);
                }
                submissionToUpdate.FilePath = "/uploads/" + uniqueFileName;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Submission/Delete/5
        public async Task<IActionResult> Delete(Guid? submissionId)
        {
            if (submissionId == null) return NotFound();
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .FirstOrDefaultAsync(m => m.SubmissionId == submissionId);
            if (submission == null) return NotFound();
            return View(submission);
        }

        // POST: Submission/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid submissionId)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission != null)
            {
                if (!string.IsNullOrEmpty(submission.FilePath))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", submission.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
                _context.Submissions.Remove(submission);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}