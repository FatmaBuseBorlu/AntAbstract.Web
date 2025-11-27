using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using Rotativa.AspNetCore; // PDF için gerekli
using System.Linq;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // Sadece üye olanlar girebilir
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public SubmissionController(AppDbContext context, UserManager<AppUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // 1. LİSTELEME SAYFASI
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var userSubmissions = await _context.Submissions
                .Where(s => s.AuthorId == user.Id)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var viewModel = new SubmissionListViewModel
            {
                Submissions = userSubmissions
            };

            return View(viewModel);
        }

        // 2. DETAY SAYFASI
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User);

            var submission = await _context.Submissions
                .Where(s => s.SubmissionId == id && s.AuthorId == userId)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Review)
                .Include(s => s.Conference) // Tenant bilgisi için
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync();

            if (submission == null)
            {
                return NotFound();
            }

            return View(submission);
        }

        // 3. YENİ BİLDİRİ FORMU (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View(new SubmissionCreateViewModel());
        }

        // 4. YENİ BİLDİRİ KAYDETME (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model)
        {
            if (model.SubmissionFile == null || model.SubmissionFile.Length == 0)
            {
                ModelState.AddModelError("SubmissionFile", "Lütfen geçerli bir dosya yükleyiniz.");
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // --- KONGRE BULMA ---
                // Gerçek senaryoda TenantContext'ten alınır, şimdilik ilkini alıyoruz
                var activeConference = await _context.Conferences.FirstOrDefaultAsync();

                if (activeConference == null)
                {
                    TempData["ErrorMessage"] = "Sistemde tanımlı bir kongre bulunamadı. Lütfen yöneticiyle iletişime geçin.";
                    return View(model);
                }

                // --- DOSYA YÜKLEME ---
                string uniqueFileName = null;
                string originalFileName = null;

                if (model.SubmissionFile != null)
                {
                    originalFileName = model.SubmissionFile.FileName;
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    string extension = Path.GetExtension(model.SubmissionFile.FileName);
                    uniqueFileName = Guid.NewGuid().ToString() + extension;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.SubmissionFile.CopyToAsync(fileStream);
                    }
                }

                // --- SUBMISSION OLUŞTURMA ---
                var submission = new Submission
                {
                    Title = model.Title,
                    AbstractText = model.AbstractText,
                    Keywords = model.Keywords,
                    AuthorId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    Status = SubmissionStatus.New,
                    ConferenceId = activeConference.Id
                };

                // --- YAZARLARI EKLEME ---
                if (model.Authors != null)
                {
                    int order = 1;
                    foreach (var authorVm in model.Authors)
                    {
                        submission.SubmissionAuthors.Add(new SubmissionAuthor
                        {
                            FirstName = authorVm.FirstName,
                            LastName = authorVm.LastName,
                            Email = authorVm.Email,
                            Institution = authorVm.Institution,
                            ORCID = authorVm.ORCID,
                            IsCorrespondingAuthor = authorVm.IsCorrespondingAuthor,
                            Order = order++
                        });
                    }
                }

                // --- DOSYA KAYDI ---
                if (uniqueFileName != null)
                {
                    var subFile = new SubmissionFile
                    {
                        FileName = originalFileName,
                        StoredFileName = uniqueFileName,
                        FilePath = "/uploads/submissions/" + uniqueFileName,
                        Type = SubmissionFileType.FullTextDoc,
                        UploadedAt = DateTime.UtcNow,
                        Version = 1
                    };
                    submission.Files.Add(subFile);
                }

                _context.Submissions.Add(submission);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Bildiriniz başarıyla gönderildi.";
                return RedirectToAction("Index", "Submission"); // Listeye dön
            }

            return View(model);
        }

        // 5. REVİZYON YÜKLEME FORMU (GET)
        [HttpGet]
        public async Task<IActionResult> UploadRevision(Guid id)
        {
            var submission = await _context.Submissions
               .FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == _userManager.GetUserId(User));

            if (submission == null || submission.Status != SubmissionStatus.RevisionRequired)
            {
                TempData["ErrorMessage"] = "Bu bildirinin revizyon süresi kapalıdır.";
                return RedirectToAction(nameof(Index));
            }

            return View(submission);
        }

        // 6. REVİZYON KAYDETME (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadRevision(Guid id, IFormFile revisionFile)
        {
            var userId = _userManager.GetUserId(User);
            var submissionToUpdate = await _context.Submissions
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == userId);

            if (submissionToUpdate == null || submissionToUpdate.Status != SubmissionStatus.RevisionRequired)
            {
                TempData["ErrorMessage"] = "Revizyon yükleme izniniz yok.";
                return RedirectToAction(nameof(Index));
            }

            if (revisionFile == null || revisionFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Lütfen bir dosya seçiniz.";
                return RedirectToAction("UploadRevision", new { id });
            }

            // Dosya Kaydetme
            try
            {
                var extension = Path.GetExtension(revisionFile.FileName);
                var newFileName = Guid.NewGuid().ToString() + "_V" + (submissionToUpdate.Files.Count + 1) + extension;
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string filePath = Path.Combine(uploadsFolder, newFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await revisionFile.CopyToAsync(stream);
                }

                var newSubmissionFile = new SubmissionFile
                {
                    FileName = revisionFile.FileName,
                    StoredFileName = newFileName,
                    FilePath = "/uploads/submissions/" + newFileName,
                    Type = SubmissionFileType.FullTextDoc,
                    UploadedAt = DateTime.UtcNow,
                    Version = submissionToUpdate.Files.Count + 1,
                    SubmissionId = submissionToUpdate.SubmissionId
                };

                submissionToUpdate.Files.Add(newSubmissionFile);
                submissionToUpdate.Status = SubmissionStatus.UnderReview; // Durumu güncelle

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Revizyon başarıyla yüklendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // 7. KABUL MEKTUBU İNDİRME (PDF)
        [HttpGet]
        public async Task<IActionResult> DownloadAcceptanceLetter(Guid id)
        {
            var userId = _userManager.GetUserId(User);

            var submission = await _context.Submissions
                .Where(s => s.SubmissionId == id && s.AuthorId == userId)
                .Include(s => s.Conference)
                .Include(s => s.Author)
                .FirstOrDefaultAsync();

            if (submission == null || submission.Status != SubmissionStatus.Accepted)
            {
                TempData["ErrorMessage"] = "Bu bildirinin kabul mektubu henüz yayınlanmamıştır.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new AcceptanceLetterViewModel
            {
                AuthorFullName = submission.Author.FirstName + " " + submission.Author.LastName,
                AuthorInstitution = submission.Author.University ?? "Kurum Bilgisi Yok",
                SubmissionTitle = submission.Title,
                ConferenceName = submission.Conference.Title,
                ConferenceStartDate = submission.Conference.StartDate,
                AcceptanceDate = submission.DecisionDate ?? DateTime.Now,
                DocumentNumber = "ACC-" + submission.SubmissionId.ToString().Substring(0, 8).ToUpper(),
                ConferenceLogoPath = submission.Conference.LogoPath
            };

            // SubmissionController.cs -> DownloadAcceptanceLetter metodu

            return new ViewAsPdf("AcceptanceLetterPreview", viewModel)
            {
                FileName = $"Certificate_{submission.SubmissionId.ToString().Substring(0, 5)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,

                // BURASI DEĞİŞTİ: Sayfayı Yan (Yatay) Yapıyoruz
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,

                // Kenar boşluklarını ayarlıyoruz (Tasarım kenara yakın)
                PageMargins = { Left = 10, Right = 10, Top = 10, Bottom = 10 },

                CustomSwitches = "--disable-smart-shrinking --background --print-media-type --enable-local-file-access"
            };
        }
    }
}