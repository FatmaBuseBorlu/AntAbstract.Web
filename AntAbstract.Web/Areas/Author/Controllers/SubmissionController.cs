using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Submissions;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Author.Controllers
{
    [Area("Author")]
    [Authorize(Roles = "Author,Admin")]
    public class SubmissionController : Controller
    {
        private readonly ISubmissionService _submissionService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IMapper _mapper;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly AppDbContext _context;
        private readonly IStringLocalizer<SubmissionController> _localizer;

        public SubmissionController(
            ISubmissionService submissionService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IMapper mapper,
            ISelectedConferenceService selectedConferenceService,
            AppDbContext context,
            IStringLocalizer<SubmissionController> localizer)
        {
            _submissionService = submissionService;
            _userManager = userManager;
            _env = env;
            _mapper = mapper;
            _selectedConferenceService = selectedConferenceService;
            _context = context;
            _localizer = localizer;
        }

        private string GetSlug()
        {
            return RouteData.Values["slug"]?.ToString()
                   ?? HttpContext.Session.GetString("SelectedConferenceSlug")
                   ?? "";
        }

        private string GetCanonicalSlug(Conference conference, string? fallbackSlug = null)
        {
            return conference.Tenant?.Slug
                   ?? conference.Slug
                   ?? fallbackSlug
                   ?? "";
        }

        private void SetSelectedConferenceSession(Conference conference, string slug)
        {
            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
        }

        private async Task<Conference?> ResolveConferenceAsync(string? slug, Guid? conferenceId = null)
        {
            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                return await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == conferenceId.Value);
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.Slug == slug ||
                        (c.Tenant != null && c.Tenant.Slug == slug));
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                return await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == selectedId.Value);
            }

            var selectedIdStr = HttpContext.Session.GetString("SelectedConferenceId");

            if (Guid.TryParse(selectedIdStr, out var parsedId))
            {
                return await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == parsedId);
            }

            return null;
        }

        private async Task<Registration?> GetUserRegistrationAsync(string userId, Guid conferenceId)
        {
            return await _context.Registrations
                .Include(r => r.RegistrationType)
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.AppUserId == userId &&
                    r.ConferenceId == conferenceId);
        }

        private async Task<IActionResult?> EnsureUserCanCreateSubmissionAsync(AppUser user, Conference conference, string canonicalSlug)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                return null;
            }

            var registration = await GetUserRegistrationAsync(user.Id, conference.Id);

            if (registration == null)
            {
                TempData["InfoMessage"] = _localizer["RegisterBeforeSubmission"].Value;

                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                {
                    return Redirect($"/{canonicalSlug}/registration");
                }

                return Redirect("/Dashboard/MyConferences");
            }

            var payableAmount = registration.Amount > 0
                ? registration.Amount
                : registration.RegistrationType?.Price ?? 0;

            if (!registration.IsPaid && payableAmount > 0)
            {
                TempData["InfoMessage"] = _localizer["CompletePaymentBeforeSubmission"].Value;

                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                {
                    return Redirect($"/{canonicalSlug}/Payment/Index/{registration.Id}");
                }

                return Redirect("/Payment/My");
            }

            return null;
        }

        private void FillSingleConferenceList(SubmissionCreateViewModel model, Conference conference)
        {
            model.AvailableConferences = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = conference.Id.ToString(),
                    Text = conference.Title,
                    Selected = true
                }
            };

            model.ConferenceId = conference.Id;
        }

        private async Task<List<SelectListItem>> GetRegisteredConferenceSelectListAsync(AppUser user, Guid? selectedConferenceId = null)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            IQueryable<Conference> query = _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking();

            if (!isAdmin)
            {
                var registeredIds = _context.Registrations
                    .AsNoTracking()
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId);

                query = query.Where(c => registeredIds.Contains(c.Id));
            }

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return conferences.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Title,
                Selected = selectedConferenceId.HasValue && c.Id == selectedConferenceId.Value
            }).ToList();
        }

        [HttpGet("/Submission/Index")]
        [HttpGet("/{slug}/Submission/Index")]
        public async Task<IActionResult> Index(string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionDtos = await _submissionService.GetMySubmissionsAsync(user.Id);

            if (!string.IsNullOrWhiteSpace(slug))
            {
                var conference = await ResolveConferenceAsync(slug);

                if (conference != null)
                {
                    ViewBag.CurrentConferenceTitle = conference.Title;
                    submissionDtos = submissionDtos
                        .Where(s => s.ConferenceId == conference.Id)
                        .ToList();
                }
            }

            return View(submissionDtos);
        }

        [HttpGet("/Submission/Details/{id:guid}")]
        [HttpGet("/{slug}/Submission/Details/{id:guid}")]
        public async Task<IActionResult> Details(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            return View(submissionDto);
        }

        [HttpGet("/Submission/Create")]
        [HttpGet("/{slug}/Submission/Create")]
        public async Task<IActionResult> Create(string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var conference = await ResolveConferenceAsync(slug);

            if (conference == null)
            {
                TempData["InfoMessage"] = _localizer["SelectConferenceBeforeSubmission"].Value;
                return Redirect("/Dashboard/MyConferences");
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference, canonicalSlug);

            var redirectResult = await EnsureUserCanCreateSubmissionAsync(user, conference, canonicalSlug);

            if (redirectResult != null)
            {
                return redirectResult;
            }

            var model = new SubmissionCreateViewModel();
            FillSingleConferenceList(model, conference);

            return View(model);
        }

        [HttpPost("/Submission/Create")]
        [HttpPost("/{slug}/Submission/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var conference = await ResolveConferenceAsync(slug, model.ConferenceId);

            if (conference == null)
            {
                ModelState.AddModelError("ConferenceId", _localizer["InvalidConferenceSelection"].Value);
                model.AvailableConferences = await GetRegisteredConferenceSelectListAsync(user, model.ConferenceId);
                return View(model);
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference, canonicalSlug);

            var redirectResult = await EnsureUserCanCreateSubmissionAsync(user, conference, canonicalSlug);

            if (redirectResult != null)
            {
                return redirectResult;
            }

            model.ConferenceId = conference.Id;
            FillSingleConferenceList(model, conference);

            if (model.SubmissionFile == null || model.SubmissionFile.Length == 0)
            {
                ModelState.AddModelError("SubmissionFile", _localizer["SubmissionFileRequired"].Value);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var fileInfo = await UploadFileAsync(model.SubmissionFile);

                var allAuthors = new List<SubmissionAuthorDto>
                {
                    new SubmissionAuthorDto
                    {
                        FirstName = user.FirstName ?? _localizer["DefaultFirstName"].Value,
                        LastName = user.LastName ?? _localizer["DefaultLastName"].Value,
                        Email = user.Email,
                        Institution = user.Institution ?? _localizer["DefaultInstitution"].Value,
                        IsCorrespondingAuthor = true,
                        Order = 1
                    }
                };

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
                    ConferenceId = conference.Id,
                    Title = model.Title,
                    Abstract = model.AbstractText,
                    Keywords = model.Keywords,
                    Topic = model.Topic,
                    PresentationType = model.PresentationType,
                    FilePath = fileInfo.FilePathDb,
                    StoredFileName = fileInfo.StoredFileName,
                    OriginalFileName = fileInfo.OriginalFileName,
                    SubmissionAuthors = allAuthors
                };

                await _submissionService.CreateSubmissionAsync(createDto, user.Id);

                TempData["SuccessMessage"] = _localizer["SubmissionCreateSuccess"].Value;

                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                {
                    return RedirectToAction(nameof(Index), new { slug = canonicalSlug });
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("SubmissionFile", ex.Message);
                FillSingleConferenceList(model, conference);
                return View(model);
            }
        }

        [HttpGet("/Submission/Edit/{id:guid}")]
        [HttpGet("/{slug}/Submission/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            if (submissionDto.Status == "Accepted" || submissionDto.Status == "Rejected")
            {
                TempData["ErrorMessage"] = _localizer["CompletedSubmissionCannotBeEdited"].Value;
                return RedirectToAction(nameof(Details), new { id, slug });
            }

            var model = new SubmissionEditViewModel
            {
                Id = submissionDto.Id,
                Title = submissionDto.Title,
                AbstractText = submissionDto.Abstract,
                Keywords = submissionDto.Keywords,
                Topic = submissionDto.Topic,
                PresentationType = submissionDto.PresentationType,
                ExistingFilePath = submissionDto.Files?
                    .OrderByDescending(f => f.UploadedAt)
                    .FirstOrDefault()
                    ?.FilePath,
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
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                string? filePath = null;
                string? storedFileName = null;
                string? originalFileName = null;

                try
                {
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
                        Topic = model.Topic,
                        PresentationType = model.PresentationType,
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

                    await _submissionService.UpdateSubmissionAsync(id, updateDto);

                    TempData["SuccessMessage"] = _localizer["SubmissionUpdateSuccess"].Value;
                    return RedirectToAction(nameof(Index), new { slug });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("SubmissionFile", ex.Message);
                }
            }

            return View(model);
        }

        [HttpGet("/Submission/Delete/{id:guid}")]
        [HttpGet("/{slug}/Submission/Delete/{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            if (submissionDto.Status != "New")
            {
                TempData["ErrorMessage"] = _localizer["ProcessedSubmissionCannotBeDeleted"].Value;
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

            TempData["SuccessMessage"] = _localizer["SubmissionDeleteSuccess"].Value;
            return RedirectToAction(nameof(Index), new { slug });
        }

        [HttpGet("/Submission/UploadRevision/{id:guid}")]
        [HttpGet("/{slug}/Submission/UploadRevision/{id:guid}")]
        public async Task<IActionResult> UploadRevision(Guid id, string? slug = null)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            if (submissionDto.Status != "RevisionRequired")
            {
                TempData["ErrorMessage"] = _localizer["RevisionPeriodClosed"].Value;
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
                TempData["ErrorMessage"] = _localizer["RevisionFileRequired"].Value;
                return RedirectToAction(nameof(UploadRevision), new { id, slug });
            }

            try
            {
                await UploadFileAsync(revisionFile);

                TempData["SuccessMessage"] = _localizer["RevisionUploadSuccess"].Value;
                return RedirectToAction(nameof(Details), new { id, slug });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(UploadRevision), new { id, slug });
            }
        }

        [HttpGet("/Submission/DownloadAcceptanceLetter/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadAcceptanceLetter/{id:guid}")]
        public async Task<IActionResult> DownloadAcceptanceLetter(Guid id)
        {
            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            if (submissionDto.Status != "Accepted" && submissionDto.Status != "Presented")
            {
                return BadRequest(_localizer["AcceptanceLetterNotReady"].Value);
            }

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

            if (submissionDto == null)
            {
                return NotFound();
            }

            if (submissionDto.Status != "Rejected")
            {
                return BadRequest(_localizer["GenericError"].Value);
            }

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

            if (submissionDto == null)
            {
                return NotFound();
            }

            if (submissionDto.Status != "Accepted" && submissionDto.Status != "Presented")
            {
                return BadRequest(_localizer["GenericError"].Value);
            }

            return new ViewAsPdf("BadgePreview", submissionDto)
            {
                FileName = $"Badge_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A6,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(0, 0, 0, 0)
            };
        }

        private async Task<(string FilePathDb, string StoredFileName, string OriginalFileName)> UploadFileAsync(IFormFile file)
        {
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                throw new Exception(_localizer["InvalidFileExtension"].Value);
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                throw new Exception(_localizer["FileTooLarge"].Value);
            }

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + extension;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return (
                "/uploads/submissions/" + uniqueFileName,
                uniqueFileName,
                file.FileName
            );
        }
    }
}