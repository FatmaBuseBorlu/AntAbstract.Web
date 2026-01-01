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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Infrastructure.Services;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class SubmissionController : Controller
    {
        private readonly ISubmissionService _submissionService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IMapper _mapper;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public SubmissionController(
            ISubmissionService submissionService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IMapper mapper,
            ISelectedConferenceService selectedConferenceService)
        {
            _submissionService = submissionService;
            _userManager = userManager;
            _env = env;
            _mapper = mapper;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Submission/Index")]
        [HttpGet("/{slug}/Submission/Index")]
        public async Task<IActionResult> Index(string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var submissionDtos = await _submissionService.GetMySubmissionsAsync(user.Id);
            return View(submissionDtos);
        }

        [HttpGet("/Submission/Details/{id:guid}")]
        [HttpGet("/{slug}/Submission/Details/{id:guid}")]
        public async Task<IActionResult> Details(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto == null) return NotFound();
            return View(submissionDto);
        }

        [HttpGet("/Submission/Create")]
        [HttpGet("/{slug}/Submission/Create")]
        public async Task<IActionResult> Create(string? slug = null)
        {
            var conferenceDtos = await _submissionService.GetActiveConferencesAsync();

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId.HasValue)
            {
                var onlySelected = conferenceDtos.Where(c => c.Id == selectedId.Value).ToList();
                if (onlySelected.Any())
                {
                    conferenceDtos = onlySelected;
                }
            }

            var selectList = conferenceDtos.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title,
                Selected = selectedId.HasValue && c.Id == selectedId.Value
            }).ToList();

            var model = new SubmissionCreateViewModel
            {
                AvailableConferences = selectList,
                ConferenceId = selectedId ?? (selectList.Any() ? Guid.Parse(selectList.First().Value) : Guid.Empty)
            };

            return View(model);
        }

        [HttpPost("/Submission/Create")]
        [HttpPost("/{slug}/Submission/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model, string? slug = null)
        {
            if (model.SubmissionFile == null || model.SubmissionFile.Length == 0)
                ModelState.AddModelError("SubmissionFile", "Lütfen dosya yükleyiniz.");

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId.HasValue)
                model.ConferenceId = selectedId.Value;

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
                return RedirectToAction(nameof(Index), new { slug });
            }

            var conferenceDtos = await _submissionService.GetActiveConferencesAsync();

            if (selectedId.HasValue)
            {
                var onlySelected = conferenceDtos.Where(c => c.Id == selectedId.Value).ToList();
                if (onlySelected.Any())
                    conferenceDtos = onlySelected;
            }

            model.AvailableConferences = conferenceDtos.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title,
                Selected = selectedId.HasValue && c.Id == selectedId.Value
            }).ToList();

            return View(model);
        }


        [HttpGet("/Submission/Edit/{id:guid}")]
        [HttpGet("/{slug}/Submission/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto == null) return NotFound();

            if (submissionDto.Status == "Accepted" || submissionDto.Status == "Rejected")
            {
                TempData["ErrorMessage"] = "Sonuçlanmış bildiriler düzenlenemez.";
                return RedirectToAction(nameof(Details), new { id, slug });
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

        [HttpPost("/Submission/Edit/{id:guid}")]
        [HttpPost("/{slug}/Submission/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, SubmissionEditViewModel model, string? slug = null)
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
                    return RedirectToAction(nameof(Index), new { slug });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Güncelleme hatası: " + ex.Message);
                }
            }

            return View(model);
        }

        [HttpGet("/Submission/Delete/{id:guid}")]
        [HttpGet("/{slug}/Submission/Delete/{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto == null) return NotFound();

            if (submissionDto.Status != "New" && submissionDto.Status != "Pending")
            {
                TempData["ErrorMessage"] = "İşlem görmüş bildiriler silinemez.";
                return RedirectToAction(nameof(Details), new { id, slug });
            }

            return View(submissionDto);
        }

        [HttpPost("/Submission/Delete/{id:guid}")]
        [HttpPost("/{slug}/Submission/Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id, string? slug = null)
        {
            await _submissionService.DeleteSubmissionAsync(id);
            TempData["SuccessMessage"] = "Bildiri başarıyla silindi.";
            return RedirectToAction(nameof(Index), new { slug });
        }

        [HttpGet("/Submission/UploadRevision/{id:guid}")]
        [HttpGet("/{slug}/Submission/UploadRevision/{id:guid}")]
        public async Task<IActionResult> UploadRevision(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto.Status != "RevisionRequired")
            {
                TempData["ErrorMessage"] = "Bu bildirinin revizyon süresi kapalıdır.";
                return RedirectToAction(nameof(Index), new { slug });
            }
            return View(submissionDto);
        }

        [HttpPost("/Submission/UploadRevision/{id:guid}")]
        [HttpPost("/{slug}/Submission/UploadRevision/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadRevision(Guid id, Microsoft.AspNetCore.Http.IFormFile revisionFile, string? slug = null)
        {
            if (revisionFile == null || revisionFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Lütfen bir dosya seçiniz.";
                return RedirectToAction(nameof(UploadRevision), new { id, slug });
            }

            await UploadFileAsync(revisionFile);
            TempData["SuccessMessage"] = "Revizyon dosyası başarıyla yüklendi.";
            return RedirectToAction(nameof(Details), new { id, slug });
        }

        [HttpPost("/Submission/UploadPresentation/{id:guid}")]
        [HttpPost("/{slug}/Submission/UploadPresentation/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPresentation(Guid id, Microsoft.AspNetCore.Http.IFormFile presentationFile, string? slug = null)
        {
            if (presentationFile == null || presentationFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Lütfen bir dosya seçiniz.";
                return RedirectToAction(nameof(Details), new { id, slug });
            }

            await UploadFileAsync(presentationFile);
            TempData["SuccessMessage"] = "Sunum dosyası başarıyla yüklendi.";
            return RedirectToAction(nameof(Details), new { id, slug });
        }

        [HttpGet("/Submission/DownloadAcceptanceLetter/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadAcceptanceLetter/{id:guid}")]
        public async Task<IActionResult> DownloadAcceptanceLetter(Guid id, string? slug = null)
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

        [HttpGet("/Submission/DownloadRejectionLetter/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadRejectionLetter/{id:guid}")]
        public async Task<IActionResult> DownloadRejectionLetter(Guid id, string? slug = null)
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

        [HttpGet("/Submission/DownloadBadge/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadBadge/{id:guid}")]
        public async Task<IActionResult> DownloadBadge(Guid id, string? slug = null)
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
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("/Submission/Withdraw/{id:guid}")]
        [HttpPost("/{slug}/Submission/Withdraw/{id:guid}")]
        public async Task<IActionResult> Withdraw(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto == null) return NotFound();

            if (submissionDto.Status != "New" && submissionDto.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Değerlendirme süreci başlayan bildiriler geri çekilemez. Lütfen yönetim ile iletişime geçin.";
                return RedirectToAction(nameof(Details), new { id, slug });
            }

            await _submissionService.DeleteSubmissionAsync(id);
            TempData["InfoMessage"] = "Bildiri başarıyla geri çekildi.";
            return RedirectToAction(nameof(Index), new { slug });
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
