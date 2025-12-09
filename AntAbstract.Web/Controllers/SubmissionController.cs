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
using System.Linq;
using System.Threading.Tasks;
using Rotativa.AspNetCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
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

        // --- LİSTELEME ---
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var userSubmissions = await _context.Submissions
                .Where(s => s.AuthorId == user.Id)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .Include(s => s.Conference)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var viewModel = new SubmissionListViewModel { Submissions = userSubmissions };
            return View(viewModel);
        }

        // --- DETAY ---
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var submission = await _context.Submissions
                .Where(s => s.SubmissionId == id && s.AuthorId == userId)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .Include(s => s.ReviewAssignments).ThenInclude(ra => ra.Review)
                .Include(s => s.Conference).ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync();

            if (submission == null) return NotFound();

            return View(submission);
        }

        // --- OLUŞTURMA (GET) ---
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var conferences = await _context.Conferences
                .OrderByDescending(c => c.StartDate)
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                { Value = c.Id.ToString(), Text = c.Title }).ToListAsync();

            return View(new SubmissionCreateViewModel { AvailableConferences = conferences });
        }

        // --- OLUŞTURMA (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model)
        {
            if (model.SubmissionFile == null || model.SubmissionFile.Length == 0)
                ModelState.AddModelError("SubmissionFile", "Lütfen dosya yükleyiniz.");

            if (model.ConferenceId == Guid.Empty)
                ModelState.AddModelError("ConferenceId", "Lütfen kongre seçiniz.");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
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

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.SubmissionFile.CopyToAsync(stream);
                    }
                }

                var submission = new Submission
                {
                    Title = model.Title,
                    AbstractText = model.AbstractText,
                    Keywords = model.Keywords,
                    AuthorId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    Status = SubmissionStatus.New,
                    ConferenceId = model.ConferenceId
                };

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

                if (uniqueFileName != null)
                {
                    submission.Files.Add(new SubmissionFile
                    {
                        FileName = originalFileName,
                        StoredFileName = uniqueFileName,
                        FilePath = "/uploads/submissions/" + uniqueFileName,
                        Type = SubmissionFileType.FullTextDoc,
                        UploadedAt = DateTime.UtcNow,
                        Version = 1
                    });
                }

                _context.Submissions.Add(submission);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Bildiriniz başarıyla gönderildi.";
                return RedirectToAction("Index");
            }

            model.AvailableConferences = await _context.Conferences.OrderByDescending(c => c.StartDate)
               .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.Id.ToString(), Text = c.Title }).ToListAsync();
            return View(model);
        }

        // --- DÜZENLEME (GET) ---
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            var submission = await _context.Submissions.Include(s => s.SubmissionAuthors)
                .FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == user.Id);

            if (submission == null) return NotFound();

            var model = new SubmissionCreateViewModel
            {
                Title = submission.Title,
                AbstractText = submission.AbstractText,
                Keywords = submission.Keywords,
                ConferenceId = submission.ConferenceId,
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
            ViewBag.SubmissionId = id;
            return View(model);
        }

        // --- DÜZENLEME (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, SubmissionCreateViewModel model)
        {
            if (model.SubmissionFile == null) ModelState.Remove("SubmissionFile");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var submission = await _context.Submissions.Include(s => s.SubmissionAuthors).Include(s => s.Files)
                    .FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == user.Id);

                if (submission == null) return NotFound();

                submission.Title = model.Title;
                submission.AbstractText = model.AbstractText;
                submission.Keywords = model.Keywords;

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
                TempData["SuccessMessage"] = "Bildiri güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.SubmissionId = id;
            return View(model);
        }

        // --- REVİZYON YÜKLEME ---
        [HttpGet]
        public async Task<IActionResult> UploadRevision(Guid id)
        {
            var submission = await _context.Submissions.FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == _userManager.GetUserId(User));
            if (submission == null || submission.Status != SubmissionStatus.RevisionRequired)
            {
                TempData["ErrorMessage"] = "Bu bildirinin revizyon süresi kapalıdır.";
                return RedirectToAction(nameof(Index));
            }
            return View(submission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadRevision(Guid id, IFormFile revisionFile)
        {
            var userId = _userManager.GetUserId(User);
            var submission = await _context.Submissions.Include(s => s.Files).FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == userId);

            if (submission == null || submission.Status != SubmissionStatus.RevisionRequired)
                return RedirectToAction(nameof(Index));

            if (revisionFile != null && revisionFile.Length > 0)
            {
                var ext = Path.GetExtension(revisionFile.FileName);
                var newFileName = Guid.NewGuid().ToString() + "_V" + (submission.Files.Count + 1) + ext;
                var path = Path.Combine(_env.WebRootPath, "uploads", "submissions", newFileName);

                using (var stream = new FileStream(path, FileMode.Create)) await revisionFile.CopyToAsync(stream);

                submission.Files.Add(new SubmissionFile
                {
                    FileName = revisionFile.FileName,
                    StoredFileName = newFileName,
                    FilePath = "/uploads/submissions/" + newFileName,
                    Type = SubmissionFileType.FullTextDoc,
                    UploadedAt = DateTime.UtcNow,
                    Version = submission.Files.Count + 1,
                    SubmissionId = submission.SubmissionId
                });

                submission.Status = SubmissionStatus.UnderReview;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Revizyon yüklendi.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- SUNUM DOSYASI YÜKLEME ---
        [HttpPost]
        public async Task<IActionResult> UploadPresentation(Guid id, IFormFile presentationFile)
        {
            var submission = await _context.Submissions.Include(s => s.Files).FirstOrDefaultAsync(s => s.SubmissionId == id);
            if (submission != null && presentationFile != null)
            {
                var ext = Path.GetExtension(presentationFile.FileName);
                var fileName = $"Presentation_{id}{ext}";
                var path = Path.Combine(_env.WebRootPath, "uploads", "presentations", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var stream = new FileStream(path, FileMode.Create)) await presentationFile.CopyToAsync(stream);

                submission.Files.Add(new SubmissionFile
                {
                    FileName = presentationFile.FileName,
                    StoredFileName = fileName,
                    FilePath = "/uploads/presentations/" + fileName,
                    Type = SubmissionFileType.Presentation,
                    UploadedAt = DateTime.UtcNow,
                    Version = 1,
                    SubmissionId = id
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Sunum dosyası yüklendi.";
            }
            return RedirectToAction("Details", new { id });
        }

        // --- GERİ ÇEKME ---
        [HttpPost]
        public async Task<IActionResult> Withdraw(Guid id)
        {
            var submission = await _context.Submissions.FindAsync(id);
            if (submission != null)
            {
                // Sadece Henüz Karar Verilmemişse Çekilebilir
                if (submission.Status == SubmissionStatus.Accepted || submission.Status == SubmissionStatus.Presented)
                {
                    TempData["ErrorMessage"] = "Kabul edilen bildiriler geri çekilemez.";
                    return RedirectToAction("Details", new { id });
                }

                submission.Status = SubmissionStatus.Withdrawn;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Bildiri geri çekildi.";
            }
            return RedirectToAction("Index");
        }

        // --- RET MEKTUBU İNDİR ---
        [HttpGet]
        public async Task<IActionResult> DownloadRejectionLetter(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(m => m.SubmissionId == id);

            if (submission == null) return NotFound();

            if (submission.Status != SubmissionStatus.Rejected)
            {
                return BadRequest("Bu belge sadece reddedilen bildiriler için oluşturulabilir.");
            }

            return new ViewAsPdf("RejectionLetter", submission)
            {
                FileName = $"Rejection_Letter_{submission.SubmissionId.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(20, 20, 20, 20)
            };
        }

        // --- KABUL MEKTUBU / SERTİFİKA İNDİR ---
        [HttpGet]
        public async Task<IActionResult> DownloadAcceptanceLetter(Guid id)
        {
            var submission = await _context.Submissions.Include(s => s.Conference).Include(s => s.Author)
                .FirstOrDefaultAsync(s => s.SubmissionId == id && s.AuthorId == _userManager.GetUserId(User));

            if (submission == null || (submission.Status != SubmissionStatus.Accepted && submission.Status != SubmissionStatus.Presented))
            {
                TempData["ErrorMessage"] = "Belge oluşturulamadı.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new AcceptanceLetterViewModel
            {
                AuthorFullName = $"{submission.Author.FirstName} {submission.Author.LastName}",
                AuthorInstitution = submission.Author.University ?? "Kurum Yok",
                SubmissionTitle = submission.Title,
                ConferenceName = submission.Conference.Title,
                ConferenceStartDate = submission.Conference.StartDate,
                AcceptanceDate = submission.DecisionDate ?? DateTime.Now,
                DocumentNumber = "DOC-" + submission.SubmissionId.ToString().Substring(0, 8).ToUpper(),
                ConferenceLogoPath = submission.Conference.LogoPath
            };

            return new ViewAsPdf("AcceptanceLetterPreview", viewModel)
            {
                FileName = $"Certificate_{submission.SubmissionId.ToString().Substring(0, 5)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageMargins = { Left = 0, Right = 0, Top = 0, Bottom = 0 },
                CustomSwitches = "--disable-smart-shrinking --background --print-media-type --enable-local-file-access"
            };
        }

        // --- YAKA KARTI İNDİR ---
        [HttpGet]
        public async Task<IActionResult> DownloadBadge(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null) return NotFound();

            if (submission.Status == SubmissionStatus.Withdrawn || submission.Status == SubmissionStatus.Rejected)
            {
                TempData["ErrorMessage"] = "Geri çekilen veya reddedilen bildiriler için yaka kartı oluşturulamaz.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            var model = new AcceptanceLetterViewModel
            {
                AuthorFullName = $"{submission.Author.FirstName} {submission.Author.LastName}",
                AuthorInstitution = submission.Author.University ?? "Kurum Bilgisi Yok",
                ConferenceName = submission.Conference.Title,
                ConferenceLogoPath = submission.Conference.LogoPath
            };

            return new ViewAsPdf("BadgePreview", model)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A6,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = { Left = 0, Right = 0, Top = 0, Bottom = 0 },
                CustomSwitches = "--disable-smart-shrinking --background --print-media-type --enable-local-file-access"
            };
        }
    }
}