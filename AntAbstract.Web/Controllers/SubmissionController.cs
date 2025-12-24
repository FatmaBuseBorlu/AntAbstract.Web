using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class SubmissionController : Controller
    {
        private readonly ISubmissionService _submissionService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IMapper _mapper;

        public SubmissionController(
            ISubmissionService submissionService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IMapper mapper)
        {
            _submissionService = submissionService;
            _userManager = userManager;
            _env = env;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var submissionDtos = await _submissionService.GetMySubmissionsAsync(user.Id);
            return View(submissionDtos);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto == null) return NotFound();
            return View(submissionDto);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var conferenceDtos = await _submissionService.GetActiveConferencesAsync();

            var selectList = conferenceDtos.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            }).ToList();

            var model = new SubmissionCreateViewModel
            {
                AvailableConferences = selectList
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model)
        {
            if (model.SubmissionFile == null || model.SubmissionFile.Length == 0)
                ModelState.AddModelError("SubmissionFile", "Lütfen dosya yükleyiniz.");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var fileInfo = await UploadFileAsync(model.SubmissionFile);

                var createDto = new CreateSubmissionDto
                {
                    ConferenceId = model.ConferenceId,
                    Title = model.Title,
                    Abstract = model.AbstractText,
                    Keywords = model.Keywords,
                    FilePath = fileInfo.FilePathDb,
                    StoredFileName = fileInfo.StoredFileName,
                    OriginalFileName = fileInfo.OriginalFileName,
                    SubmissionAuthors = model.Authors.Select(a => new SubmissionAuthorDto
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

                await _submissionService.CreateSubmissionAsync(createDto, user.Id);

                TempData["SuccessMessage"] = "Bildiriniz başarıyla gönderildi.";
                return RedirectToAction("Index");
            }

            var conferenceDtos = await _submissionService.GetActiveConferencesAsync();
            model.AvailableConferences = conferenceDtos.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            if (submissionDto.Status == "Accepted" || submissionDto.Status == "Rejected")
            {
                TempData["ErrorMessage"] = "Sonuçlanmış bildiriler düzenlenemez.";
                return RedirectToAction("Details", new { id = id });
            }

            var model = new SubmissionEditViewModel
            {
                Id = submissionDto.Id,
                Title = submissionDto.Title,
                AbstractText = submissionDto.Abstract,
                Keywords = submissionDto.Keywords,
                ExistingFilePath = submissionDto.Files?.OrderByDescending(f => f.UploadedAt).FirstOrDefault()?.FilePath,

                Authors = submissionDto.Authors.Select(a => new SubmissionAuthorViewModel
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

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, SubmissionEditViewModel model)
        {
            if (id != model.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                string filePath = null;
                string storedFileName = null;
                string originalFileName = null;

                if (model.SubmissionFile != null && model.SubmissionFile.Length > 0)
                {
                    var fileInfo = await UploadFileAsync(model.SubmissionFile);
                    filePath = fileInfo.FilePathDb;
                    storedFileName = fileInfo.StoredFileName;
                    originalFileName = fileInfo.OriginalFileName;
                }

                var updateDto = new CreateSubmissionDto
                {
                    Title = model.Title,
                    Abstract = model.AbstractText,
                    Keywords = model.Keywords,
                    FilePath = filePath,
                    StoredFileName = storedFileName,
                    OriginalFileName = originalFileName,
                    SubmissionAuthors = model.Authors.Select(a => new SubmissionAuthorDto
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

                try
                {
                    await _submissionService.UpdateSubmissionAsync(id, updateDto);
                    TempData["SuccessMessage"] = "Bildiri başarıyla güncellendi.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Güncelleme hatası: " + ex.Message);
                }
            }

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Delete(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto == null) return NotFound();

            if (submissionDto.Status != "New" && submissionDto.Status != "Pending")
            {
                TempData["ErrorMessage"] = "İşlem görmüş bildiriler silinemez.";
                return RedirectToAction("Details", new { id = id });
            }

            return View(submissionDto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            await _submissionService.DeleteSubmissionAsync(id);
            TempData["SuccessMessage"] = "Bildiri başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> UploadRevision(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto.Status != "RevisionRequired")
            {
                TempData["ErrorMessage"] = "Bu bildirinin revizyon süresi kapalıdır.";
                return RedirectToAction(nameof(Index));
            }
            return View(submissionDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadRevision(Guid id, Microsoft.AspNetCore.Http.IFormFile revisionFile)
        {
            if (revisionFile == null || revisionFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Lütfen bir dosya seçiniz.";
                return RedirectToAction(nameof(UploadRevision), new { id = id });
            }

            var fileInfo = await UploadFileAsync(revisionFile);

            TempData["SuccessMessage"] = "Revizyon dosyası başarıyla yüklendi.";
            return RedirectToAction("Details", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPresentation(Guid id, Microsoft.AspNetCore.Http.IFormFile presentationFile)
        {
            if (presentationFile == null || presentationFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Lütfen bir dosya seçiniz.";
                return RedirectToAction("Details", new { id = id });
            }

            var fileInfo = await UploadFileAsync(presentationFile);


            TempData["SuccessMessage"] = "Sunum dosyası başarıyla yüklendi.";
            return RedirectToAction("Details", new { id = id });
        }


        [HttpGet]
        public async Task<IActionResult> DownloadAcceptanceLetter(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto.Status != "Accepted" && submissionDto.Status != "Presented")
                return BadRequest("Bu belge henüz oluşmamıştır.");

            return new ViewAsPdf("AcceptanceLetterPreview", submissionDto)
            {
                FileName = $"Certificate_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }

        [HttpGet]
        public async Task<IActionResult> DownloadRejectionLetter(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto.Status != "Rejected")
                return BadRequest("Bu belge sadece reddedilen bildiriler için geçerlidir.");

            return new ViewAsPdf("RejectionLetter", submissionDto)
            {
                FileName = $"Rejection_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        [HttpGet]
        public async Task<IActionResult> DownloadBadge(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto.Status != "Accepted" && submissionDto.Status != "Presented")
                return BadRequest("Yaka kartı için bildiri kabul edilmiş olmalıdır.");

            return new ViewAsPdf("BadgePreview", submissionDto)
            {
                FileName = $"Badge_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A6,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(0, 0, 0, 0)
            };
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Editor")]
        public async Task<IActionResult> ChangeStatus(Guid id, string status, string note)
        {

            TempData["SuccessMessage"] = "Bildiri durumu güncellendi.";
            return RedirectToAction("Details", new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> Withdraw(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null) return NotFound();

            if (submissionDto.Status != "New" && submissionDto.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Değerlendirme süreci başlayan bildiriler geri çekilemez. Lütfen yönetim ile iletişime geçin.";
                return RedirectToAction("Details", new { id = id });
            }

            await _submissionService.DeleteSubmissionAsync(id);

            TempData["InfoMessage"] = "Bildiri başarıyla geri çekildi.";
            return RedirectToAction("Index");
        }

        private async Task<(string FilePathDb, string StoredFileName, string OriginalFileName)> UploadFileAsync(Microsoft.AspNetCore.Http.IFormFile file)
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string extension = Path.GetExtension(file.FileName);
            string uniqueFileName = Guid.NewGuid().ToString() + extension;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return ("/uploads/submissions/" + uniqueFileName, uniqueFileName, file.FileName);
        }
    }
}