using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
                .Include(s => s.Conference)
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
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync();

            if (submission == null)
            {
                return NotFound();
            }

            return View(submission);
        }

        // 1. YENİ BİLDİRİ FORMU (GET) - LİSTEYİ DOLDURARAK AÇ
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Aktif kongreleri çekip Dropdown için hazırlıyoruz
            var conferences = await _context.Conferences
                .OrderByDescending(c => c.StartDate)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                }).ToListAsync();

            var model = new SubmissionCreateViewModel
            {
                AvailableConferences = conferences
            };

            return View(model);
        }

        // 2. YENİ BİLDİRİ KAYDETME (POST) - SEÇİLEN KONGREYE KAYDET
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model)
        {
            // Dosya seçilmemişse hata ver
            if (model.SubmissionFile == null || model.SubmissionFile.Length == 0)
            {
                ModelState.AddModelError("SubmissionFile", "Lütfen geçerli bir dosya yükleyiniz.");
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // --- HATA DÜZELTİLDİ: SEÇİLEN KONGREYİ KULLAN ---
                // Kullanıcının formdan seçtiği ConferenceId'yi kullanıyoruz.
                // Eğer Tenant bazlı çalışıyorsanız burada Tenant kontrolü de yapılabilir.

                if (model.ConferenceId == Guid.Empty)
                {
                    ModelState.AddModelError("ConferenceId", "Lütfen bir kongre seçiniz.");
                    // Listeyi tekrar doldur (Hata durumunda boş gelmesin)
                    model.AvailableConferences = await _context.Conferences
                        .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title }).ToListAsync();
                    return View(model);
                }

                // --- DOSYA YÜKLEME ---
                string uniqueFileName = null;
                string originalFileName = null;

                if (model.SubmissionFile != null)
                {
                    originalFileName = model.SubmissionFile.FileName;
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

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

                    // DÜZELTME BURADA: Rastgele değil, seçilen ID'yi atıyoruz.
                    ConferenceId = model.ConferenceId
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
                return RedirectToAction("Index", "Submission");
            }

            // Hata varsa dropdown listesini tekrar doldur
            model.AvailableConferences = await _context.Conferences
                .OrderByDescending(c => c.StartDate)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            return View(model);
        }

        // 5. DÜZENLEME (EDIT) - GET
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            var submission = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == user.Id);

            if (submission == null) return NotFound();

            //// Eğer bildiri işlem görmüşse (UnderReview vb.) düzenlemeye kapatılabilir
            //if (submission.Status != SubmissionStatus.New && submission.Status != SubmissionStatus.RevisionRequired)
            //{
            //    TempData["ErrorMessage"] = "Bu bildiri değerlendirme sürecinde olduğu için düzenlenemez.";
            //    return RedirectToAction(nameof(Index));
            //}

            var model = new SubmissionCreateViewModel
            {
                Title = submission.Title,
                AbstractText = submission.AbstractText,
                Keywords = submission.Keywords,
                Authors = submission.SubmissionAuthors.OrderBy(a => a.Order).Select(a => new SubmissionAuthorViewModel
                {
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    Email = a.Email,
                    Institution = a.Institution,
                    ORCID = a.ORCID,
                    IsCorrespondingAuthor = a.IsCorrespondingAuthor,
                    Order = a.Order
                }).ToList()
            };

            ViewBag.SubmissionId = id; // View'a ID taşımak için
            return View(model);
        }

        // 6. DÜZENLEME (EDIT) - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, SubmissionCreateViewModel model)
        {
            // Dosya zorunlu değil (yüklemezse eskisi kalır)
            if (model.SubmissionFile == null) ModelState.Remove("SubmissionFile");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var submission = await _context.Submissions
                    .Include(s => s.SubmissionAuthors)
                    .Include(s => s.Files)
                    .FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == user.Id);

                if (submission == null) return NotFound();

                // Güncelleme
                submission.Title = model.Title;
                submission.AbstractText = model.AbstractText;
                submission.Keywords = model.Keywords;

                // Yazarları Güncelle (Sil ve Yeniden Ekle)
                _context.RemoveRange(submission.SubmissionAuthors);
                if (model.Authors != null)
                {
                    int order = 1;
                    foreach (var authorVm in model.Authors)
                    {
                        submission.SubmissionAuthors.Add(new SubmissionAuthor
                        {
                            SubmissionId = submission.SubmissionId,
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

                // Dosya Güncelleme (Varsa yeni versiyon ekle)
                if (model.SubmissionFile != null)
                {
                    string extension = Path.GetExtension(model.SubmissionFile.FileName);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_EDIT" + extension;
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.SubmissionFile.CopyToAsync(fileStream);
                    }

                    submission.Files.Add(new SubmissionFile
                    {
                        FileName = model.SubmissionFile.FileName,
                        StoredFileName = uniqueFileName,
                        FilePath = "/uploads/submissions/" + uniqueFileName,
                        Type = SubmissionFileType.FullTextDoc,
                        UploadedAt = DateTime.UtcNow,
                        Version = submission.Files.Count + 1
                    });
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Bildiri başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.SubmissionId = id;
            return View(model);
        }

        // 7. REVİZYON YÜKLEME FORMU (GET)
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

        // 8. REVİZYON KAYDETME (POST)
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

        // 9. KABUL MEKTUBU İNDİRME (PDF)
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

            return new ViewAsPdf("AcceptanceLetterPreview", viewModel)
            {
                FileName = $"Certificate_{submission.SubmissionId.ToString().Substring(0, 5)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageMargins = { Left = 10, Right = 10, Top = 10, Bottom = 10 },
                CustomSwitches = "--disable-smart-shrinking --background --print-media-type --enable-local-file-access --viewport-size 1280x1024"
            };
        }
    }
}