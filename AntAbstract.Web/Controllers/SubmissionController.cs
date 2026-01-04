using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context; // DbContext için
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels.Admin.Submissions;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly AppDbContext _context;

        public SubmissionController(
            ISubmissionService submissionService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IMapper mapper,
            ISelectedConferenceService selectedConferenceService,
            AppDbContext context)
        {
            _submissionService = submissionService;
            _userManager = userManager;
            _env = env;
            _mapper = mapper;
            _selectedConferenceService = selectedConferenceService;
            _context = context;
        }

        [HttpGet("/Submission/Index")]
        [HttpGet("/{slug}/Submission/Index")]
        public async Task<IActionResult> Index(string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            var submissionDtos = await _submissionService.GetMySubmissionsAsync(user.Id);

            if (!string.IsNullOrEmpty(slug))
            {
                var conference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Slug == slug || (c.Tenant != null && c.Tenant.Slug == slug));

                if (conference != null)
                {
                    ViewBag.CurrentConferenceTitle = conference.Title;

                    submissionDtos = submissionDtos.Where(s => s.ConferenceId == conference.Id).ToList();
                }
            }

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

            if (!selectedId.HasValue && !string.IsNullOrEmpty(slug))
            {
                var confEntity = await _context.Conferences
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Slug == slug || (c.Tenant != null && c.Tenant.Slug == slug));

                if (confEntity != null) selectedId = confEntity.Id;
            }

            var selectList = conferenceDtos.Select(c => new SelectListItem
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

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var fileInfo = await UploadFileAsync(model.SubmissionFile);

                string presentationTypeStr = model.PresentationTypeId == 1 ? "Poster" : "Oral";
                string finalKeywords = string.IsNullOrEmpty(model.Keywords)
                    ? presentationTypeStr
                    : model.Keywords + ", " + presentationTypeStr;

                var allAuthors = new List<SubmissionAuthorDto>();

                allAuthors.Add(new SubmissionAuthorDto
                {
                    FirstName = user.FirstName ?? "Ad",
                    LastName = user.LastName ?? "Soyad",
                    Email = user.Email,
                    Institution = "Kurum Belirtilmedi",
                    IsCorrespondingAuthor = true,
                    Order = 1
                });

                if (model.Authors != null && model.Authors.Any())
                {
                    int orderCounter = 2;
                    foreach (var authorVm in model.Authors)
                    {
                        allAuthors.Add(new SubmissionAuthorDto
                        {
                            FirstName = authorVm.FirstName,
                            LastName = authorVm.LastName,
                            Email = authorVm.Email,
                            Institution = authorVm.Institution,
                            ORCID = authorVm.ORCID,
                            IsCorrespondingAuthor = authorVm.IsCorrespondingAuthor,
                            Order = orderCounter++
                        });
                    }
                }

                var createDto = new CreateSubmissionDto
                {
                    ConferenceId = model.ConferenceId,
                    Title = model.Title,
                    Abstract = model.AbstractText,
                    Keywords = finalKeywords,
                    FilePath = fileInfo.FilePathDb,
                    StoredFileName = fileInfo.StoredFileName,
                    OriginalFileName = fileInfo.OriginalFileName,
                    SubmissionAuthors = allAuthors
                };

                await _submissionService.CreateSubmissionAsync(createDto, user.Id);

                TempData["SuccessMessage"] = "Bildiriniz başarıyla gönderildi.";
                return RedirectToAction(nameof(Index), new { slug });
            }

            var conferenceDtos = await _submissionService.GetActiveConferencesAsync();
            model.AvailableConferences = conferenceDtos.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title,
                Selected = model.ConferenceId == c.Id
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
                    SubmissionAuthors = model.Authors?.Select(a => new SubmissionAuthorDto
                    {
                        FirstName = a.FirstName,
                        LastName = a.LastName,
                        Email = a.Email,
                        Institution = a.Institution,
                        ORCID = a.ORCID,
                        IsCorrespondingAuthor = a.IsCorrespondingAuthor,
                        Order = a.Order
                    }).ToList() ?? new List<SubmissionAuthorDto>()
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

            if (submissionDto.Status != "New")
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
        public async Task<IActionResult> UploadRevision(Guid id, IFormFile revisionFile, string? slug = null)
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

        [HttpGet("/Submission/DownloadAcceptanceLetter/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadAcceptanceLetter/{id:guid}")]
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

        [HttpGet("/Submission/DownloadRejectionLetter/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadRejectionLetter/{id:guid}")]
        public async Task<IActionResult> DownloadRejectionLetter(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto.Status != "Rejected") return BadRequest("Hata");

            return new ViewAsPdf("RejectionLetter", submissionDto)
            {
                FileName = $"Rejection_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        [HttpGet("/Submission/DownloadBadge/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadBadge/{id:guid}")]
        public async Task<IActionResult> DownloadBadge(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);
            if (submissionDto.Status != "Accepted" && submissionDto.Status != "Presented") return BadRequest("Hata");

            return new ViewAsPdf("BadgePreview", submissionDto)
            {
                FileName = $"Badge_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A6,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(0, 0, 0, 0)
            };
        }

        private async Task<(string FilePathDb, string StoredFileName, string OriginalFileName)> UploadFileAsync(IFormFile file)
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