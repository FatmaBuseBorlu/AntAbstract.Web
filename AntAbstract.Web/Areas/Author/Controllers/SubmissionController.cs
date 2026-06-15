using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Files;
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Author.Controllers
{
    [Area("Author")]
    [Authorize(Roles = "Author,Yazar,Admin")]
    public class SubmissionController : Controller
    {
        private readonly ISubmissionService _submissionService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IMapper _mapper;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly AppDbContext _context;
        private readonly IStringLocalizer<SubmissionController> _localizer;
        private readonly INotificationService _notificationService;
        private readonly IUploadFileValidator _uploadFileValidator;

        public SubmissionController(
            ISubmissionService submissionService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IMapper mapper,
            ISelectedConferenceService selectedConferenceService,
            AppDbContext context,
            IStringLocalizer<SubmissionController> localizer,
            INotificationService notificationService,
            IUploadFileValidator uploadFileValidator)
        {
            _submissionService = submissionService;
            _userManager = userManager;
            _env = env;
            _mapper = mapper;
            _selectedConferenceService = selectedConferenceService;
            _context = context;
            _localizer = localizer;
            _notificationService = notificationService;
            _uploadFileValidator = uploadFileValidator;
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

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private static string BuildUrl(string? slug, string path)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return path;
            }

            return $"/{slug}{path}";
        }

        private static bool SlugMatches(Conference? conference, string? slug)
        {
            if (conference == null || string.IsNullOrWhiteSpace(slug))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(conference.Slug) &&
                string.Equals(conference.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (conference.Tenant != null &&
                !string.IsNullOrWhiteSpace(conference.Tenant.Slug) &&
                string.Equals(conference.Tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private Guid? GetSelectedConferenceIdFromSession(Guid? tenantId = null)
        {
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                var tenantSpecificValue = HttpContext.Session.GetString(
                    $"SelectedConferenceId:{tenantId.Value}");

                if (Guid.TryParse(tenantSpecificValue, out var tenantSpecificConferenceId) &&
                    tenantSpecificConferenceId != Guid.Empty)
                {
                    return tenantSpecificConferenceId;
                }
            }

            var globalValue = HttpContext.Session.GetString("SelectedConferenceId");

            if (Guid.TryParse(globalValue, out var globalConferenceId) &&
                globalConferenceId != Guid.Empty)
            {
                return globalConferenceId;
            }

            return null;
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

        private async Task<Conference?> FindConferenceByIdAsync(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conferenceId);
        }

        private async Task<Conference?> ResolveConferenceAsync(string? slug, Guid? conferenceId = null)
        {
            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                var conferenceById = await FindConferenceByIdAsync(conferenceId.Value);

                if (conferenceById != null && !SlugMatches(conferenceById, slug))
                {
                    return null;
                }

                return conferenceById;
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedConference = await FindConferenceByIdAsync(selectedId.Value);

                if (selectedConference != null && SlugMatches(selectedConference, slug))
                {
                    return selectedConference;
                }
            }

            Guid? sessionConferenceId = null;

            if (!string.IsNullOrWhiteSpace(slug))
            {
                var slugConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .Where(c =>
                        c.Slug == slug ||
                        (c.Tenant != null && c.Tenant.Slug == slug))
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefaultAsync();

                if (slugConference != null)
                {
                    sessionConferenceId = GetSelectedConferenceIdFromSession(slugConference.TenantId);

                    if (sessionConferenceId.HasValue)
                    {
                        var selectedConferenceFromTenantSession = await FindConferenceByIdAsync(sessionConferenceId.Value);

                        if (selectedConferenceFromTenantSession != null &&
                            SlugMatches(selectedConferenceFromTenantSession, slug))
                        {
                            return selectedConferenceFromTenantSession;
                        }
                    }

                    return slugConference;
                }
            }

            sessionConferenceId ??= GetSelectedConferenceIdFromSession();

            if (sessionConferenceId.HasValue)
            {
                var selectedConferenceFromSession = await FindConferenceByIdAsync(sessionConferenceId.Value);

                if (selectedConferenceFromSession != null && SlugMatches(selectedConferenceFromSession, slug))
                {
                    return selectedConferenceFromSession;
                }
            }

            return null;
        }

        private async Task<Submission?> GetAuthorizedSubmissionAsync(
            Guid id,
            AppUser user,
            bool asNoTracking = true)
        {
            var query = _context.Submissions
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .AsQueryable();

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            var submission = await query.FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null)
            {
                return null;
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            // Baş yazar veya ortak yazar (co-author) ise erişim izni var
            var isCoAuthor = submission.SubmissionAuthors?.Any(a => a.AppUserId == user.Id) == true;

            if (!isAdmin && submission.AuthorId != user.Id && !isCoAuthor)
            {
                return null;
            }

            return submission;
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

        private async Task<IActionResult?> EnsureUserCanCreateSubmissionAsync(
            AppUser user,
            Conference conference,
            string canonicalSlug)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isAdmin)
            {
                return null; // Admin her zaman bildiri gönderebilir
            }

            // Bildiri başvuruları kapalı mı?
            if (!conference.IsSubmissionOpen)
            {
                TempData["ErrorMessage"] = T(
                    "SubmissionsClosed",
                    "Bu kongre için bildiri başvuruları kapatılmıştır.");

                return Redirect(BuildUrl(canonicalSlug, ""));
            }

            // Son başvuru tarihi geçmiş mi?
            var now = DateTime.UtcNow;
            if (conference.FullTextSubmissionDeadline.HasValue &&
                now > conference.FullTextSubmissionDeadline.Value)
            {
                TempData["ErrorMessage"] = T(
                    "SubmissionDeadlinePassed",
                    $"Bildiri gönderim süresi {conference.FullTextSubmissionDeadline.Value:dd.MM.yyyy} tarihinde sona ermiştir.");

                return Redirect(BuildUrl(canonicalSlug, ""));
            }

            if (conference.AbstractSubmissionDeadline.HasValue &&
                !conference.FullTextSubmissionDeadline.HasValue &&
                now > conference.AbstractSubmissionDeadline.Value)
            {
                TempData["ErrorMessage"] = T(
                    "AbstractDeadlinePassed",
                    $"Özet gönderim süresi {conference.AbstractSubmissionDeadline.Value:dd.MM.yyyy} tarihinde sona ermiştir.");

                return Redirect(BuildUrl(canonicalSlug, ""));
            }

            var registration = await GetUserRegistrationAsync(user.Id, conference.Id);

            if (registration == null)
            {
                TempData["InfoMessage"] = T(
                    "RegisterBeforeSubmission",
                    "Bildiri gönderebilmek için önce kongreye kayıt olmalısınız.");

                if (!string.IsNullOrWhiteSpace(canonicalSlug))
                {
                    return Redirect(BuildUrl(canonicalSlug, "/registration"));
                }

                return Redirect("/Dashboard/MyConferences");
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

        private async Task<List<SelectListItem>> GetConferenceTopicSelectListAsync(
            Conference conference,
            Guid? selectedTopicId = null)
        {
            var isEnglish = CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);

            var topics = await _context.ConferenceTopics
                .AsNoTracking()
                .Where(t =>
                    t.ConferenceId == conference.Id &&
                    t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Name)
                .ToListAsync();

            return topics.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = isEnglish && !string.IsNullOrWhiteSpace(t.NameEn)
                    ? t.NameEn
                    : t.Name,
                Selected = selectedTopicId.HasValue && t.Id == selectedTopicId.Value
            }).ToList();
        }

        private async Task<List<SelectListItem>> GetRegisteredConferenceSelectListAsync(
            AppUser user,
            Guid? selectedConferenceId = null)
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
        [HttpGet("/{slug}/my-submissions")]
        public async Task<IActionResult> Index(string? slug = null, string? statusFilter = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                TempData["InfoMessage"] = T(
                    "SelectConferenceBeforeSubmissionList",
                    "Bildiri işlemleri için önce bir kongre seçmelisiniz.");

                return Redirect("/Dashboard/MyConferences");
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var conference = await ResolveConferenceAsync(slug);

            if (conference == null)
            {
                TempData["InfoMessage"] = T(
                    "SelectConferenceBeforeSubmissionList",
                    "Bildiri işlemleri için önce bir kongre seçmelisiniz.");

                return Redirect("/Dashboard/MyConferences");
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

            ViewBag.CurrentConferenceTitle = conference.Title;

            var hasCompletedAttendance = await _context.ConferenceAttendances
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ConferenceId == conference.Id &&
                    x.UserId == user.Id &&
                    (
                        x.CompletedAt.HasValue ||
                        x.TotalSeconds >= x.RequiredSeconds
                    ));

            ViewBag.CanDownloadAuthorCertificate = hasCompletedAttendance;

            var submissionDtos = await _submissionService.GetMySubmissionsAsync(user.Id);

            submissionDtos = submissionDtos
                .Where(s => s.ConferenceId == conference.Id)
                .ToList();

            // Status filter
            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                Enum.TryParse<SubmissionStatus>(statusFilter, out var parsedStatus))
            {
                submissionDtos = submissionDtos.Where(s =>
                {
                    Enum.TryParse<SubmissionStatus>(s.Status, out var ss);
                    return ss == parsedStatus;
                }).ToList();
            }

            ViewBag.StatusFilter = statusFilter ?? "";
            ViewBag.AllCount = submissionDtos.Count;

            return View(submissionDtos);
        }

        [HttpGet("/Submission/Details/{id:guid}")]
        [HttpGet("/{slug}/Submission/Details/{id:guid}")]
        [HttpGet("/{slug}/my-submissions/{id:guid}")]
        public async Task<IActionResult> Details(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await GetAuthorizedSubmissionAsync(id, user);

            if (submissionEntity == null)
            {
                return Forbid();
            }

            if (!SlugMatches(submissionEntity.Conference, slug))
            {
                var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}"));
            }

            var canonical = GetCanonicalSlug(submissionEntity.Conference!, slug);

            if (string.IsNullOrWhiteSpace(slug))
            {
                return Redirect(BuildUrl(canonical, $"/my-submissions/{id}"));
            }

            SetSelectedConferenceSession(submissionEntity.Conference!, canonical);

            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            return View(submissionDto);
        }

        [HttpGet("/Submission/Create")]
        [HttpGet("/{slug}/Submission/Create")]
        [HttpGet("/{slug}/submit-abstract")]
        public async Task<IActionResult> Create(string? slug = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                TempData["InfoMessage"] = T(
                    "SelectConferenceBeforeSubmission",
                    "Bildiri göndermeden önce bir kongre seçmelisiniz.");

                return Redirect("/Dashboard/MyConferences");
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var conference = await ResolveConferenceAsync(slug);

            if (conference == null)
            {
                TempData["InfoMessage"] = T(
                    "SelectConferenceBeforeSubmission",
                    "Bildiri göndermeden önce bir kongre seçmelisiniz.");

                return Redirect("/Dashboard/MyConferences");
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(BuildUrl(canonicalSlug, "/submit-abstract"));
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

            var redirectResult = await EnsureUserCanCreateSubmissionAsync(user, conference, canonicalSlug);

            if (redirectResult != null)
            {
                return redirectResult;
            }

            var model = new SubmissionCreateViewModel();

            FillSingleConferenceList(model, conference);
            model.AvailableTopics = await GetConferenceTopicSelectListAsync(conference);

            return View(model);
        }

        [HttpPost("/Submission/Create")]
        [HttpPost("/{slug}/Submission/Create")]
        [HttpPost("/{slug}/submit-abstract")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionCreateViewModel model, string? slug = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                TempData["InfoMessage"] = T(
                    "SelectConferenceBeforeSubmission",
                    "Bildiri göndermeden önce bir kongre seçmelisiniz.");

                return Redirect("/Dashboard/MyConferences");
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var conference = await ResolveConferenceAsync(slug, model.ConferenceId);

            if (conference == null)
            {
                ModelState.AddModelError(
                    nameof(model.ConferenceId),
                    T("InvalidConferenceSelection", "Geçersiz kongre seçimi."));

                model.AvailableConferences = await GetRegisteredConferenceSelectListAsync(user, model.ConferenceId);
                model.AvailableTopics = new List<SelectListItem>();

                return View(model);
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(BuildUrl(canonicalSlug, "/submit-abstract"));
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

            var redirectResult = await EnsureUserCanCreateSubmissionAsync(user, conference, canonicalSlug);

            if (redirectResult != null)
            {
                return redirectResult;
            }

            model.ConferenceId = conference.Id;

            FillSingleConferenceList(model, conference);

            model.AvailableTopics = await GetConferenceTopicSelectListAsync(
                conference,
                model.ConferenceTopicId
            );

            ConferenceTopic? selectedTopic = null;

            if (!model.ConferenceTopicId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.ConferenceTopicId),
                    T("TopicRequired", "Lütfen bildiri konusunu seçiniz."));
            }
            else
            {
                selectedTopic = await _context.ConferenceTopics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t =>
                        t.Id == model.ConferenceTopicId.Value &&
                        t.ConferenceId == conference.Id &&
                        t.IsActive);

                if (selectedTopic == null)
                {
                    ModelState.AddModelError(
                        nameof(model.ConferenceTopicId),
                        T("InvalidTopicSelection", "Geçersiz bildiri konusu seçimi."));
                }
            }

            if (model.SubmissionFile == null || model.SubmissionFile.Length == 0)
            {
                ModelState.AddModelError(
                    nameof(model.SubmissionFile),
                    T("SubmissionFileRequired", "Bildiri dosyası zorunludur."));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var fileInfo = await UploadFileAsync(model.SubmissionFile!);

                var allAuthors = new List<SubmissionAuthorDto>
                {
                    new SubmissionAuthorDto
                    {
                        FirstName = user.FirstName ?? T("DefaultFirstName", "Ad"),
                        LastName = user.LastName ?? T("DefaultLastName", "Soyad"),
                        Email = user.Email ?? "",
                        Institution = user.Institution ?? T("DefaultInstitution", "Kurum belirtilmedi"),
                        IsCorrespondingAuthor = true,
                        Order = 1
                    }
                };

                if (model.Authors != null && model.Authors.Any())
                {
                    int orderCounter = 2;

                    foreach (var authorVm in model.Authors)
                    {
                        var hasAnyAuthorValue =
                            !string.IsNullOrWhiteSpace(authorVm.FirstName) ||
                            !string.IsNullOrWhiteSpace(authorVm.LastName) ||
                            !string.IsNullOrWhiteSpace(authorVm.Email) ||
                            !string.IsNullOrWhiteSpace(authorVm.Institution) ||
                            !string.IsNullOrWhiteSpace(authorVm.ORCID);

                        if (!hasAnyAuthorValue)
                        {
                            continue;
                        }

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
                    ConferenceTopicId = selectedTopic!.Id,

                    Title = model.Title,
                    Abstract = model.AbstractText,
                    Keywords = model.Keywords,

                    Topic = selectedTopic.Name,
                    PresentationType = model.PresentationType,

                    FilePath = fileInfo.FilePathDb,
                    StoredFileName = fileInfo.StoredFileName,
                    OriginalFileName = fileInfo.OriginalFileName,

                    SubmissionAuthors = allAuthors
                };

                await _submissionService.CreateSubmissionAsync(createDto, user.Id);

                TempData["SuccessMessage"] = T(
                    "SubmissionCreateSuccess",
                    "Bildiriniz başarıyla gönderildi.");

                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(model.SubmissionFile), ex.Message);

                FillSingleConferenceList(model, conference);

                model.AvailableTopics = await GetConferenceTopicSelectListAsync(
                    conference,
                    model.ConferenceTopicId
                );

                return View(model);
            }
        }

        [HttpGet("/Submission/Edit/{id:guid}")]
        [HttpGet("/{slug}/Submission/Edit/{id:guid}")]
        [HttpGet("/{slug}/my-submissions/{id:guid}/edit")]
        public async Task<IActionResult> Edit(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            var submissionEntity = await _context.Submissions
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(s => s.Files)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submissionEntity == null)
            {
                return NotFound();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin && submissionEntity.AuthorId != user.Id)
            {
                return Forbid();
            }

            var conference = submissionEntity.Conference
                ?? await ResolveConferenceAsync(slug, submissionEntity.ConferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "InvalidConferenceSelection",
                    "Geçersiz kongre seçimi.");

                return Redirect(BuildUrl(slug, "/my-submissions"));
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (string.IsNullOrWhiteSpace(slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/edit"));
            }

            if (!SlugMatches(conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/edit"));
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

            if (submissionDto.Status == "Accepted" || submissionDto.Status == "Rejected")
            {
                TempData["ErrorMessage"] = T(
                    "CompletedSubmissionCannotBeEdited",
                    "Kabul edilmiş veya reddedilmiş bildiriler düzenlenemez.");

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}"));
            }

            string normalizedPresentationType = submissionDto.PresentationType switch
            {
                "0" => "Oral",
                "1" => "Poster",
                _ => string.IsNullOrWhiteSpace(submissionDto.PresentationType)
                    ? "Oral"
                    : submissionDto.PresentationType
            };

            var model = new SubmissionEditViewModel
            {
                Id = submissionDto.Id,

                ConferenceId = submissionEntity.ConferenceId,
                ConferenceTopicId = submissionEntity.ConferenceTopicId,

                Title = submissionDto.Title,
                AbstractText = submissionDto.Abstract,
                Keywords = submissionDto.Keywords,

                Topic = submissionDto.Topic,
                PresentationType = normalizedPresentationType,

                ExistingFilePath = submissionEntity.Files?
                    .OrderByDescending(f => f.UploadedAt)
                    .FirstOrDefault()
                    ?.FilePath ?? "",

                ExistingFileId = submissionEntity.Files?
                    .OrderByDescending(f => f.UploadedAt)
                    .FirstOrDefault()
                    ?.Id ?? 0,

                Authors = submissionDto.Authors?.Select(a => new SubmissionAuthorViewModel
                {
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    Email = a.Email,
                    Institution = a.Institution,
                    ORCID = a.ORCID,
                    IsCorrespondingAuthor = a.IsCorrespondingAuthor,
                    Order = a.Order
                }).ToList() ?? new List<SubmissionAuthorViewModel>()
            };

            FillSingleConferenceList(model, conference);

            model.AvailableTopics = await GetConferenceTopicSelectListAsync(
                conference,
                submissionEntity.ConferenceTopicId
            );

            return View(model);
        }

        [HttpPost("/Submission/Edit/{id:guid}")]
        [HttpPost("/{slug}/Submission/Edit/{id:guid}")]
        [HttpPost("/{slug}/my-submissions/{id:guid}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, SubmissionEditViewModel model, string? slug = null)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await _context.Submissions
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(s => s.Files)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submissionEntity == null)
            {
                return NotFound();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin && submissionEntity.AuthorId != user.Id)
            {
                return Forbid();
            }

            var conference = submissionEntity.Conference
                ?? await ResolveConferenceAsync(slug, submissionEntity.ConferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "InvalidConferenceSelection",
                    "Geçersiz kongre seçimi.");

                return Redirect(BuildUrl(slug, "/my-submissions"));
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!SlugMatches(conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/edit"));
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

            if (submissionEntity.Status == SubmissionStatus.Accepted ||
                submissionEntity.Status == SubmissionStatus.Rejected)
            {
                TempData["ErrorMessage"] = T(
                    "CompletedSubmissionCannotBeEdited",
                    "Kabul edilmiş veya reddedilmiş bildiriler düzenlenemez.");

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}"));
            }

            model.ConferenceId = conference.Id;

            FillSingleConferenceList(model, conference);

            model.ExistingFilePath = submissionEntity.Files?
                .OrderByDescending(f => f.UploadedAt)
                .FirstOrDefault()
                ?.FilePath ?? "";

            model.ExistingFileId = submissionEntity.Files?
                .OrderByDescending(f => f.UploadedAt)
                .FirstOrDefault()
                ?.Id ?? 0;

            model.AvailableTopics = await GetConferenceTopicSelectListAsync(
                conference,
                model.ConferenceTopicId
            );

            ConferenceTopic? selectedTopic = null;

            if (!model.ConferenceTopicId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.ConferenceTopicId),
                    T("TopicRequired", "Lütfen bildiri konusunu seçiniz."));
            }
            else
            {
                selectedTopic = await _context.ConferenceTopics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t =>
                        t.Id == model.ConferenceTopicId.Value &&
                        t.ConferenceId == conference.Id &&
                        t.IsActive);

                if (selectedTopic == null)
                {
                    ModelState.AddModelError(
                        nameof(model.ConferenceTopicId),
                        T("InvalidTopicSelection", "Geçersiz bildiri konusu seçimi."));
                }
            }

            var allowedPresentationTypes = new[] { "Oral", "Poster", "Online" };

            if (string.IsNullOrWhiteSpace(model.PresentationType) ||
                !allowedPresentationTypes.Contains(model.PresentationType))
            {
                ModelState.AddModelError(
                    nameof(model.PresentationType),
                    T("InvalidPresentationType", "Geçersiz sunum türü."));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

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
                    ConferenceId = conference.Id,
                    ConferenceTopicId = selectedTopic!.Id,

                    Title = model.Title,
                    Abstract = model.AbstractText,
                    Keywords = model.Keywords,

                    Topic = selectedTopic.Name,
                    PresentationType = model.PresentationType,

                    FilePath = filePath,
                    StoredFileName = storedFileName,
                    OriginalFileName = originalFileName,

                    SubmissionAuthors = model.Authors?
                        .Where(a =>
                            !string.IsNullOrWhiteSpace(a.FirstName) ||
                            !string.IsNullOrWhiteSpace(a.LastName) ||
                            !string.IsNullOrWhiteSpace(a.Email) ||
                            !string.IsNullOrWhiteSpace(a.Institution) ||
                            !string.IsNullOrWhiteSpace(a.ORCID))
                        .Select((a, index) => new SubmissionAuthorDto
                        {
                            FirstName = a.FirstName,
                            LastName = a.LastName,
                            Email = a.Email,
                            Institution = a.Institution,
                            ORCID = a.ORCID,
                            IsCorrespondingAuthor = a.IsCorrespondingAuthor,
                            Order = index + 1
                        }).ToList() ?? new List<SubmissionAuthorDto>()
                };

                await _submissionService.UpdateSubmissionAsync(id, updateDto);

                TempData["SuccessMessage"] = T(
                    "SubmissionUpdateSuccess",
                    "Bildiri başarıyla güncellendi.");

                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(model.SubmissionFile), ex.Message);

                FillSingleConferenceList(model, conference);

                model.AvailableTopics = await GetConferenceTopicSelectListAsync(
                    conference,
                    model.ConferenceTopicId
                );

                model.ExistingFilePath = submissionEntity.Files?
                    .OrderByDescending(f => f.UploadedAt)
                    .FirstOrDefault()
                    ?.FilePath ?? "";

                model.ExistingFileId = submissionEntity.Files?
                    .OrderByDescending(f => f.UploadedAt)
                    .FirstOrDefault()
                    ?.Id ?? 0;

                return View(model);
            }
        }

        [HttpGet("/Submission/Delete/{id:guid}")]
        [HttpGet("/{slug}/Submission/Delete/{id:guid}")]
        [HttpGet("/{slug}/my-submissions/{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await GetAuthorizedSubmissionAsync(id, user);

            if (submissionEntity == null)
            {
                return Forbid();
            }

            if (!SlugMatches(submissionEntity.Conference, slug))
            {
                var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/delete"));
            }

            if (string.IsNullOrWhiteSpace(slug))
            {
                var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/delete"));
            }

            var currentSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

            SetSelectedConferenceSession(submissionEntity.Conference!, currentSlug);

            if (submissionEntity.Status != SubmissionStatus.New)
            {
                TempData["ErrorMessage"] = T(
                    "ProcessedSubmissionCannotBeDeleted",
                    "İşleme alınmış bildiriler silinemez.");

                return Redirect(BuildUrl(currentSlug, $"/my-submissions/{id}"));
            }

            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            return View(submissionDto);
        }

        [HttpPost("/Submission/Delete/{id:guid}")]
        [HttpPost("/{slug}/Submission/Delete/{id:guid}")]
        [HttpPost("/{slug}/my-submissions/{id:guid}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await GetAuthorizedSubmissionAsync(id, user);

            if (submissionEntity == null)
            {
                return Forbid();
            }

            if (!SlugMatches(submissionEntity.Conference, slug))
            {
                var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/delete"));
            }

            var currentSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

            SetSelectedConferenceSession(submissionEntity.Conference!, currentSlug);

            if (submissionEntity.Status != SubmissionStatus.New)
            {
                TempData["ErrorMessage"] = T(
                    "ProcessedSubmissionCannotBeDeleted",
                    "İşleme alınmış bildiriler silinemez.");

                return Redirect(BuildUrl(currentSlug, $"/my-submissions/{id}"));
            }

            await _submissionService.DeleteSubmissionAsync(id);

            TempData["SuccessMessage"] = T(
                "SubmissionDeleteSuccess",
                "Bildiri başarıyla silindi.");

            return Redirect(BuildUrl(currentSlug, "/my-submissions"));
        }

        [HttpGet("/Submission/UploadRevision/{id:guid}")]
        [HttpGet("/{slug}/Submission/UploadRevision/{id:guid}")]
        [HttpGet("/{slug}/my-submissions/{id:guid}/revision")]
        public async Task<IActionResult> UploadRevision(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await GetAuthorizedSubmissionAsync(id, user);

            if (submissionEntity == null)
            {
                return Forbid();
            }

            if (!SlugMatches(submissionEntity.Conference, slug))
            {
                var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/revision"));
            }

            if (string.IsNullOrWhiteSpace(slug))
            {
                var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/revision"));
            }

            var currentSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

            SetSelectedConferenceSession(submissionEntity.Conference!, currentSlug);

            if (submissionEntity.Status != SubmissionStatus.RevisionRequired)
            {
                TempData["ErrorMessage"] = T(
                    "RevisionPeriodClosed",
                    "Revizyon yükleme süresi kapalı.");

                return Redirect(BuildUrl(currentSlug, "/my-submissions"));
            }

            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            return View(submissionDto);
        }

        [HttpPost("/Submission/UploadRevision/{id:guid}")]
        [HttpPost("/{slug}/Submission/UploadRevision/{id:guid}")]
        [HttpPost("/{slug}/my-submissions/{id:guid}/revision")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadRevision(Guid id, IFormFile? revisionFile, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submission = await GetAuthorizedSubmissionAsync(id, user, asNoTracking: false);

            if (submission == null)
            {
                return Forbid();
            }

            var canonicalSlug = GetCanonicalSlug(submission.Conference!, slug);

            if (!SlugMatches(submission.Conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/revision"));
            }

            SetSelectedConferenceSession(submission.Conference!, canonicalSlug);

            if (submission.Status != SubmissionStatus.RevisionRequired)
            {
                TempData["ErrorMessage"] = T(
                    "RevisionPeriodClosed",
                    "Revizyon yükleme süresi kapalı.");

                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }

            if (revisionFile == null || revisionFile.Length == 0)
            {
                TempData["ErrorMessage"] = T(
                    "RevisionFileRequired",
                    "Revizyon dosyası zorunludur.");

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/revision"));
            }

            try
            {
                var fileInfo = await UploadFileAsync(revisionFile);

                var newVersion = submission.Files != null && submission.Files.Any()
                    ? submission.Files.Max(f => f.Version) + 1
                    : 1;

                submission.Files ??= new List<SubmissionFile>();

                submission.Files.Add(new SubmissionFile
                {
                    SubmissionId = submission.Id,
                    FileName = fileInfo.OriginalFileName,
                    StoredFileName = fileInfo.StoredFileName,
                    FilePath = fileInfo.FilePathDb,
                    Type = SubmissionFileType.FullText,
                    UploadedAt = DateTime.UtcNow,
                    Version = newVersion
                });

                submission.Status = SubmissionStatus.UnderReview;
                submission.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Konferans adminine bildirim gönder
                try
                {
                    var conferenceId = submission.ConferenceId;
                    var adminUsers = await _context.Users
                        .AsNoTracking()
                        .Where(u => u.TenantId.HasValue &&
                            _context.Conferences
                                .Any(c => c.Id == conferenceId && c.TenantId == u.TenantId.Value))
                        .Select(u => u.Id)
                        .ToListAsync();

                    foreach (var adminId in adminUsers)
                    {
                        await _notificationService.CreateAsync(
                            userId: adminId,
                            title: "Revizyon Yüklendi",
                            message: $"\"{submission.Title}\" bildirisine revizyon dosyası yüklendi.",
                            icon: "📤",
                            color: "info",
                            link: null);
                    }
                }
                catch { }

                TempData["SuccessMessage"] = T(
                    "RevisionUploadSuccess",
                    "Revizyon dosyası başarıyla yüklendi.");

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}"));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/revision"));
            }
        }

        [HttpGet("/Submission/DownloadAcceptanceLetter/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadAcceptanceLetter/{id:guid}")]
        [HttpGet("/{slug}/my-submissions/{id:guid}/acceptance-letter")]
        public async Task<IActionResult> DownloadAcceptanceLetter(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await GetAuthorizedSubmissionAsync(id, user);

            if (submissionEntity == null)
            {
                return Forbid();
            }

            var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

            if (!SlugMatches(submissionEntity.Conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/acceptance-letter"));
            }

            SetSelectedConferenceSession(submissionEntity.Conference!, canonicalSlug);

            if (submissionEntity.Status != SubmissionStatus.Accepted &&
                submissionEntity.Status != SubmissionStatus.Presented)
            {
                return BadRequest(T(
                    "AcceptanceLetterNotReady",
                    "Kabul mektubu henüz hazır değil."));
            }

            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
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
        [HttpGet("/{slug}/my-submissions/{id:guid}/rejection-letter")]
        public async Task<IActionResult> DownloadRejectionLetter(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await GetAuthorizedSubmissionAsync(id, user);

            if (submissionEntity == null)
            {
                return Forbid();
            }

            var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

            if (!SlugMatches(submissionEntity.Conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/rejection-letter"));
            }

            SetSelectedConferenceSession(submissionEntity.Conference!, canonicalSlug);

            if (submissionEntity.Status != SubmissionStatus.Rejected)
            {
                return BadRequest(T(
                    "GenericError",
                    "Bu işlem şu anda yapılamaz."));
            }

            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            return new ViewAsPdf("RejectionLetter", submissionDto)
            {
                FileName = $"Rejection_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        [HttpGet("/Submission/DownloadBadge/{id:guid}")]
        [HttpGet("/{slug}/Submission/DownloadBadge/{id:guid}")]
        [HttpGet("/{slug}/my-submissions/{id:guid}/badge")]
        public async Task<IActionResult> DownloadBadge(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var submissionEntity = await GetAuthorizedSubmissionAsync(id, user);

            if (submissionEntity == null)
            {
                return Forbid();
            }

            var canonicalSlug = GetCanonicalSlug(submissionEntity.Conference!, slug);

            if (!SlugMatches(submissionEntity.Conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/badge"));
            }

            SetSelectedConferenceSession(submissionEntity.Conference!, canonicalSlug);

            if (submissionEntity.Status != SubmissionStatus.Accepted &&
                submissionEntity.Status != SubmissionStatus.Presented)
            {
                return BadRequest(T(
                    "GenericError",
                    "Bu işlem şu anda yapılamaz."));
            }

            var submissionDto = await _submissionService.GetSubmissionByIdAsync(id);

            if (submissionDto == null)
            {
                return NotFound();
            }

            return new ViewAsPdf("BadgePreview", submissionDto)
            {
                FileName = $"Badge_{submissionDto.Id.ToString().Substring(0, 8)}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A6,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(0, 0, 0, 0)
            };
        }

        // ── Withdraw ──────────────────────────────────────────────────────────────

        [HttpGet("/Submission/Withdraw/{id:guid}")]
        [HttpGet("/{slug}/Submission/Withdraw/{id:guid}")]
        [HttpGet("/{slug}/my-submissions/{id:guid}/withdraw")]
        public async Task<IActionResult> Withdraw(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await GetAuthorizedSubmissionAsync(id, user);
            if (submission == null) return Forbid();

            var canonicalSlug = GetCanonicalSlug(submission.Conference!, slug);
            if (!SlugMatches(submission.Conference, slug))
                return Redirect(BuildUrl(canonicalSlug, $"/my-submissions/{id}/withdraw"));

            SetSelectedConferenceSession(submission.Conference!, canonicalSlug);

            var withdrawableStatuses = new[]
            {
                SubmissionStatus.New,
                SubmissionStatus.Pending,
                SubmissionStatus.UnderReview,
                SubmissionStatus.RevisionRequired
            };

            if (!withdrawableStatuses.Contains(submission.Status))
            {
                TempData["ErrorMessage"] = "Kabul veya reddedilmiş bildiriler geri çekilemez.";
                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }

            ViewBag.Submission = submission;
            ViewBag.Slug = canonicalSlug;
            return View(submission);
        }

        [HttpPost("/Submission/Withdraw/{id:guid}")]
        [HttpPost("/{slug}/Submission/Withdraw/{id:guid}")]
        [HttpPost("/{slug}/my-submissions/{id:guid}/withdraw")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WithdrawConfirmed(Guid id, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await GetAuthorizedSubmissionAsync(id, user);
            if (submission == null) return Forbid();

            var canonicalSlug = GetCanonicalSlug(submission.Conference!, slug);

            var withdrawableStatuses = new[]
            {
                SubmissionStatus.New,
                SubmissionStatus.Pending,
                SubmissionStatus.UnderReview,
                SubmissionStatus.RevisionRequired
            };

            if (!withdrawableStatuses.Contains(submission.Status))
            {
                TempData["ErrorMessage"] = "Kabul veya reddedilmiş bildiriler geri çekilemez.";
                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }

            submission.Status = SubmissionStatus.Withdrawn;
            _context.Submissions.Update(submission);
            await _context.SaveChangesAsync();

            // Admin bildirim
            try
            {
                var conferenceId = submission.ConferenceId;
                var adminUsers = await _context.Users
                    .Where(u => u.TenantId.HasValue &&
                        _context.Conferences
                            .Any(c => c.Id == conferenceId && c.TenantId == u.TenantId.Value))
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var adminId in adminUsers)
                {
                    await _notificationService.CreateAsync(
                        userId: adminId,
                        title: "Bildiri Geri Çekildi",
                        message: $"\"{submission.Title}\" bildirisi yazar tarafından geri çekildi.",
                        icon: "🔙",
                        color: "warning",
                        link: null);
                }
            }
            catch { }

            TempData["SuccessMessage"] = "Bildiriniz başarıyla geri çekildi.";
            return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
        }

        // ─────────────────────────────────────────────────────────────────────────

        private async Task<(string FilePathDb, string StoredFileName, string OriginalFileName)> UploadFileAsync(IFormFile file)
        {
            var validation = await _uploadFileValidator.ValidateAsync(
                file,
                UploadFileProfile.SubmissionDocument);

            if (!validation.IsValid)
            {
                var errorMessage = validation.Error switch
                {
                    UploadValidationError.TooLarge => T(
                        "FileTooLarge",
                        "Dosya boyutu en fazla 10 MB olabilir."),
                    UploadValidationError.InvalidExtension => T(
                        "InvalidFileExtension",
                        "Sadece PDF, DOC ve DOCX dosyaları yüklenebilir."),
                    _ => T(
                        "InvalidFileContent",
                        "Dosya içeriği seçilen formatla eşleşmiyor.")
                };

                throw new InvalidOperationException(errorMessage);
            }

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = _uploadFileValidator.CreateStoredFileName(
                validation.Extension,
                "submission");
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return (
                "/uploads/submissions/" + uniqueFileName,
                uniqueFileName,
                validation.SafeOriginalFileName
            );
        }
    }
}
