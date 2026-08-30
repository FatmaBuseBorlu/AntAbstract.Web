using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.Doi;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Infrastructure.Services.Plagiarism;
using AntAbstract.Web.Files;
using AntAbstract.Web.Models.ViewModels.Admin.Submissions;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class SubmissionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISubmissionService _submissionService;
        private readonly IReviewService _reviewService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SubmissionsController> _localizer;
        private readonly IUploadFileValidator _uploadFileValidator;
        private readonly IAuditService _audit;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly ILogger<SubmissionsController> _logger;
        private readonly IPlagiarismService _plagiarism;
        private readonly AntAbstract.Infrastructure.Services.Doi.IDoiRegistrationService _doiRegistration;
        private readonly IDoiService _doiService;

        public SubmissionsController(
            AppDbContext context,
            ISubmissionService submissionService,
            IReviewService reviewService,
            UserManager<AppUser> userManager,
            IAdminTenantAccessService tenantAccess,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IWebHostEnvironment env,
            IStringLocalizer<SubmissionsController> localizer,
            IUploadFileValidator uploadFileValidator,
            IAuditService audit,
            INotificationService notificationService,
            IEmailService emailService,
            ILogger<SubmissionsController> logger,
            IPlagiarismService plagiarism,
            IDoiService doiService,
            AntAbstract.Infrastructure.Services.Doi.IDoiRegistrationService doiRegistration)
        {
            _context = context;
            _submissionService = submissionService;
            _reviewService = reviewService;
            _userManager = userManager;
            _tenantAccess = tenantAccess;
            _tenantContext = tenantContext;
            _logger = logger;
            _selectedConferenceService = selectedConferenceService;
            _env = env;
            _localizer = localizer;
            _uploadFileValidator = uploadFileValidator;
            _audit = audit;
            _notificationService = notificationService;
            _emailService = emailService;
            _plagiarism = plagiarism;
            _doiService = doiService;
            _doiRegistration = doiRegistration;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private bool IsSuperAdminUser()
        {
            return _tenantAccess.IsSuperAdmin(User);
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CurrentAdminHasTenantAsync()
        {
            if (IsSuperAdminUser())
            {
                return true;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            return tenantId.HasValue;
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string? slug)
        {
            if (IsSuperAdminUser())
            {
                return true;
            }

            return await _tenantAccess.CanAccessCurrentTenantAsync(
                User,
                slug,
                allowSuperAdmin: false);
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var query = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            return query
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();
        }

        private async Task<Conference?> GetAccessibleConferenceAsync(
            string? slug,
            Guid? conferenceId)
        {
            Guid? selectedConferenceId = null;

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                selectedConferenceId = conferenceId.Value;
            }
            else
            {
                selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            }

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                return null;
            }

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return await query.FirstOrDefaultAsync(c =>
                        c.Id == selectedConferenceId.Value &&
                        c.Tenant != null &&
                        c.Tenant.Slug == slug);
                }

                return await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value);
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return null;
            }

            if (!await CanAccessCurrentTenantAsync(slug))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == tenantId.Value &&
                    c.Tenant != null &&
                    c.Tenant.Slug == slug);
            }

            return await query.FirstOrDefaultAsync(c =>
                c.Id == selectedConferenceId.Value &&
                c.TenantId == tenantId.Value);
        }

        private async Task<Submission?> GetAccessibleSubmissionAsync(
            Guid submissionId,
            string? slug = null,
            Guid? conferenceId = null,
            bool asNoTracking = true)
        {
            var query = _context.Submissions
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(s => s.Author)
                .Include(s => s.Files)
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Reviewer)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Review)
                .AsQueryable();

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (IsSuperAdminUser())
            {
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    query = query.Where(s =>
                        s.Conference != null &&
                        s.Conference.Tenant != null &&
                        s.Conference.Tenant.Slug == slug);
                }

                if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                {
                    query = query.Where(s => s.ConferenceId == conferenceId.Value);
                }

                return await query.FirstOrDefaultAsync(s => s.Id == submissionId);
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return null;
            }

            query = query.Where(s =>
                s.Conference != null &&
                s.Conference.TenantId == tenantId.Value);

            if (!string.IsNullOrWhiteSpace(slug))
            {
                query = query.Where(s =>
                    s.Conference != null &&
                    s.Conference.Tenant != null &&
                    s.Conference.Tenant.Slug == slug);
            }

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                query = query.Where(s => s.ConferenceId == conferenceId.Value);
            }

            return await query.FirstOrDefaultAsync(s => s.Id == submissionId);
        }

        private async Task<bool> CanAccessSubmissionAsync(
            Guid submissionId,
            string? slug = null,
            Guid? conferenceId = null)
        {
            var submission = await GetAccessibleSubmissionAsync(
                submissionId,
                slug,
                conferenceId);

            return submission != null;
        }

        private async Task LoadAvailableConferencesAsync(SubmissionCreateViewModel model)
        {
            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            model.AvailableConferences = conferences
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                })
                .ToList();
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? _tenantContext.Current?.Slug ?? "";
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        private string BuildSubmissionsUrl(string? slug, Guid? conferenceId)
        {
            if (!string.IsNullOrWhiteSpace(slug) &&
                conferenceId.HasValue &&
                conferenceId.Value != Guid.Empty)
            {
                return $"/{slug}/Admin/Submissions?conferenceId={conferenceId.Value}";
            }

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                return $"/Admin/Submissions?conferenceId={conferenceId.Value}";
            }

            return "/Admin/Submissions";
        }

        private string BuildSubmissionDetailsUrl(string? slug, Guid submissionId, Guid? conferenceId)
        {
            if (!string.IsNullOrWhiteSpace(slug))
            {
                var query = conferenceId.HasValue && conferenceId.Value != Guid.Empty
                    ? $"?conferenceId={conferenceId.Value}"
                    : "";

                return $"/{slug}/Admin/Submissions/Details/{submissionId}{query}";
            }

            return $"/Admin/Submissions/Details/{submissionId}";
        }

            // Bu ekranın görünümü hiç yazılmamış: Create.cshtml depoda yok ve
            // panelde buraya giden bağlantı da bulunmuyor. Adres elle girilince
            // "view not found" ile 500 dönüyordu. Görünüm eklenene kadar çökmek
            // yerine bulunamadı demek doğrusu; eylem ve modeli olduğu gibi duruyor.
        private IActionResult SubmissionCreateUnavailable() => NotFound(T(
            "Error_SubmissionCreateUnavailable",
            "Yönetici tarafından bildiri oluşturma ekranı henüz hazır değil."));

        [HttpGet("/Admin/Submissions/Create")]
        [HttpGet("/{slug}/Admin/Submissions/Create")]
        public async Task<IActionResult> Create(
            string? slug = null,
            Guid? conferenceId = null)
        {
            var model = new SubmissionCreateViewModel();

            await LoadAvailableConferencesAsync(model);

            if (!string.IsNullOrWhiteSpace(slug) ||
                (conferenceId.HasValue && conferenceId.Value != Guid.Empty))
            {
                var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

                if (conference == null)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_UnauthorizedConference",
                        "Bu kongre için bildiri oluşturma yetkiniz yok.");

                    return RedirectToAction(nameof(SelectConference));
                }

                SetSelectedConferenceSession(conference);

                model.ConferenceId = conference.Id;
            }

            return SubmissionCreateUnavailable();
        }

        [HttpPost("/Admin/Submissions/Create")]
        [HttpPost("/{slug}/Admin/Submissions/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            SubmissionCreateViewModel model,
            string? slug = null)
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Challenge();
            }

            if (!IsSuperAdminUser() && !currentUser.TenantId.HasValue)
            {
                ModelState.AddModelError(
                    "ConferenceId",
                    T("Error_AdminTenantNotFound", "Admin hesabınıza bağlı kurum bulunamadı."));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == model.ConferenceId);

            if (conference == null)
            {
                ModelState.AddModelError(
                    "ConferenceId",
                    T("Error_InvalidConferenceSelection", "Geçersiz kongre seçimi veya bu kongreye erişim yetkiniz yok."));
            }

            if (conference != null &&
                !string.IsNullOrWhiteSpace(slug) &&
                conference.Tenant != null &&
                !string.Equals(conference.Tenant.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    "ConferenceId",
                    T("Error_InvalidConferenceSelection", "Seçilen kongre ile URL kongresi eşleşmiyor."));
            }

            if (!ModelState.IsValid)
            {
                await LoadAvailableConferencesAsync(model);

                return SubmissionCreateUnavailable();
            }

            var newSubmission = new Submission
            {
                Id = Guid.NewGuid(),
                Title = model.Title,
                Abstract = model.AbstractText,
                Keywords = model.Keywords,
                Topic = model.Topic ?? "",
                PresentationType = model.PresentationType,
                ConferenceId = model.ConferenceId,
                TenantId = conference!.TenantId,
                AuthorId = currentUser.Id,
                Status = SubmissionStatus.New,
                CreatedDate = DateTime.UtcNow
            };

            if (model.SubmissionFile != null && model.SubmissionFile.Length > 0)
            {
                var validation = await _uploadFileValidator.ValidateAsync(
                    model.SubmissionFile,
                    UploadFileProfile.SubmissionDocument);

                if (!validation.IsValid)
                {
                    var errorMessage = validation.Error switch
                    {
                        UploadValidationError.TooLarge =>
                            T("Error_FileTooLarge", "Dosya boyutu en fazla 10 MB olabilir."),
                        UploadValidationError.InvalidExtension =>
                            T("Error_InvalidFileExtension", "Sadece PDF, DOC ve DOCX dosyaları yüklenebilir."),
                        _ =>
                            T("Error_InvalidFileContent", "Dosya içeriği seçilen formatla eşleşmiyor.")
                    };

                    ModelState.AddModelError(
                        nameof(model.SubmissionFile),
                        errorMessage);

                    await LoadAvailableConferencesAsync(model);

                    return SubmissionCreateUnavailable();
                }

                var uploadsFolder = PrivateStorage.EnsureFolder(_env, PrivateStorage.SubmissionsFolder);

                var uniqueFileName = _uploadFileValidator.CreateStoredFileName(
                    validation.Extension,
                    "submission");
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.SubmissionFile.CopyToAsync(fileStream);
                }

                newSubmission.Files.Add(new SubmissionFile
                {
                    FileName = validation.SafeOriginalFileName,
                    StoredFileName = uniqueFileName,
                    FilePath = PrivateStorage.ToRelativePath(PrivateStorage.SubmissionsFolder, uniqueFileName),
                    UploadedAt = DateTime.UtcNow
                });
            }

            newSubmission.SubmissionAuthors.Add(new SubmissionAuthor
            {
                FirstName = currentUser.FirstName ?? currentUser.UserName ?? "",
                LastName = currentUser.LastName ?? "",
                Email = currentUser.Email ?? "",
                Institution = currentUser.Institution ?? T("DefaultInstitution", "Belirtilmedi"),
                IsCorrespondingAuthor = true,
                Order = 1
            });

            if (model.Authors != null && model.Authors.Any())
            {
                foreach (var author in model.Authors)
                {
                    newSubmission.SubmissionAuthors.Add(new SubmissionAuthor
                    {
                        FirstName = author.FirstName,
                        LastName = author.LastName,
                        Email = author.Email,
                        Institution = author.Institution,
                        ORCID = author.ORCID,
                        IsCorrespondingAuthor = author.IsCorrespondingAuthor,
                        Order = author.Order > 0 ? author.Order : 2
                    });
                }
            }

            _context.Submissions.Add(newSubmission);
            await _context.SaveChangesAsync();

            SetSelectedConferenceSession(conference);

            TempData["SuccessMessage"] = T(
                "Success_SubmissionCreated",
                "Bildiri başarıyla oluşturuldu.");

            var redirectSlug = conference.Tenant?.Slug ?? slug;

            return Redirect(BuildSubmissionsUrl(redirectSlug, conference.Id));
        }

        [HttpGet("/Admin/Submissions")]
        public async Task<IActionResult> SelectConference(
            Guid? conferenceId = null,
            string? returnUrl = null)
        {
            if (!await CurrentAdminHasTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                var selectableQuery = await GetAccessibleConferenceQueryAsync();

                var selectableConference = await selectableQuery
                    .FirstOrDefaultAsync(c => c.Id == conferenceId.Value);

                if (selectableConference != null)
                {
                    SetSelectedConferenceSession(selectableConference);
                }
            }

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConference?.Tenant?.Slug != null)
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect(BuildSubmissionsUrl(
                        selectedConference.Tenant.Slug,
                        selectedConference.Id));
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            if (!conferences.Any())
            {
                TempData["ErrorMessage"] = IsSuperAdminUser()
                    ? T("Msg_SistemdeGoruntulenebilecekKongreBulunamadi", "Sistemde görüntülenebilecek kongre bulunamadı.")
                    : T("Msg_KurumunuzaBagliGoruntulenebilecekKongreBulunamadi", "Kurumunuza bağlı görüntülenebilecek kongre bulunamadı.");
            }

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = IsSuperAdminUser()
                    ? T("Msg_SuperAdminOlarakSistemdekiTumKongreleriGorebilirsiniz", "SuperAdmin olarak sistemdeki tüm kongreleri görebilirsiniz. Başvurularını incelemek istediğiniz kongreyi seçiniz.")
                    : T("SelectConference_Lead", "Başvuruları yönetmek için önce kongre seçiniz."),
                PostUrl = "/Admin/Submissions/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Submissions/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(
            Guid conferenceId,
            string? returnUrl = null)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null ||
                conference.Tenant == null ||
                string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect(BuildSubmissionsUrl(
                conference.Tenant.Slug,
                conference.Id));
        }

        [HttpGet("/{slug}/Admin/Submissions")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId = null,
            string? search = null,
            string? status = null,
            string? topic = null,
            int page = 1)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedTenant",
                    "Bu kongrenin bildirilerini görüntüleme yetkiniz yok.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Submissions" });
            }

            SetSelectedConferenceSession(conference);

            // Conference-wide counts (no filters applied)
            var allBaseQuery = _context.Submissions
                .AsNoTracking()
                .Where(x => x.ConferenceId == conference.Id);

            var allCount = await allBaseQuery.CountAsync();
            var newCount = await allBaseQuery.CountAsync(x => x.Status == SubmissionStatus.New);
            var underReviewCount = await allBaseQuery.CountAsync(x => x.Status == SubmissionStatus.UnderReview);
            var acceptedCount = await allBaseQuery.CountAsync(x => x.Status == SubmissionStatus.Accepted);
            var rejectedCount = await allBaseQuery.CountAsync(x => x.Status == SubmissionStatus.Rejected);

            // Available topics for filter dropdown
            var availableTopics = await _context.Submissions
                .AsNoTracking()
                .Where(x => x.ConferenceId == conference.Id && x.Topic != null && x.Topic != "")
                .Select(x => x.Topic)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            // Filtered query
            var query = _context.Submissions
                .AsNoTracking()
                .Include(s => s.Conference)
                .Include(s => s.Author)
                .Where(x => x.ConferenceId == conference.Id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchText = search.Trim();

                query = query.Where(x =>
                    (x.Title != null && x.Title.Contains(searchText)) ||
                    (x.Author != null && (
                        (x.Author.FirstName != null && x.Author.FirstName.Contains(searchText)) ||
                        (x.Author.LastName != null && x.Author.LastName.Contains(searchText)) ||
                        (x.Author.Email != null && x.Author.Email.Contains(searchText))
                    ))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<SubmissionStatus>(status, out var parsedStatus))
            {
                query = query.Where(x => x.Status == parsedStatus);
            }

            if (!string.IsNullOrWhiteSpace(topic))
            {
                query = query.Where(x => x.Topic == topic);
            }

            const int pageSize = 50;
            if (page < 1) page = 1;

            var orderedQuery = query.OrderByDescending(x => x.CreatedDate);
            var totalCount = await orderedQuery.CountAsync();

            var submissionIds = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.Id)
                .ToListAsync();

            var reviewerCountsBySubmission = await _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => submissionIds.Contains(ra.SubmissionId))
                .GroupBy(ra => ra.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.Count);

            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminSubmissionRowModel
                {
                    Id = x.Id,
                    Title = x.Title ?? "",
                    AuthorName = x.Author == null
                        ? ""
                        : ((x.Author.FirstName ?? "") + " " + (x.Author.LastName ?? "")).Trim(),
                    ConferenceTitle = x.Conference == null ? "" : (x.Conference.Title ?? ""),
                    CreatedAt = x.CreatedDate,
                    Status = x.Status.ToString(),
                    Topic = x.Topic ?? ""
                })
                .ToListAsync();

            foreach (var item in items)
            {
                if (reviewerCountsBySubmission.TryGetValue(item.Id, out var count))
                    item.AssignedReviewerCount = count;
            }

            var model = new AdminSubmissionsIndexModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                Search = search,
                Status = status,
                Topic = topic,
                Items = items,
                AvailableTopics = availableTopics,
                AllCount = allCount,
                NewCount = newCount,
                UnderReviewCount = underReviewCount,
                AcceptedCount = acceptedCount,
                RejectedCount = rejectedCount,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View("~/Areas/Admin/Views/Submissions/Index.cshtml", model);
        }

        [HttpGet("/Admin/Submissions/Incomplete")]
        [HttpGet("/{slug}/Admin/Submissions/Incomplete")]
        public async Task<IActionResult> Incomplete(
            string? slug = null,
            Guid? conferenceId = null,
            string? missingFilter = null,
            int page = 1)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);
            if (conference == null) return RedirectToAction("SelectConference");

            var baseQuery = _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id &&
                            s.TenantId == conference.TenantId &&
                            s.Status == SubmissionStatus.New)
                .Include(s => s.Author)
                .Include(s => s.Files);

            var all = await baseQuery.ToListAsync();

            var rows = all.Select(s => new IncompleteSubmissionRow
            {
                Id = s.Id,
                SubmissionIdCode = s.SubmissionIdCode ?? s.Id.ToString()[..8],
                Title = s.Title,
                AuthorName = $"{s.Author?.FirstName} {s.Author?.LastName}".Trim(),
                AuthorEmail = s.Author?.Email ?? "",
                CreatedDate = s.CreatedDate,
                UpdatedDate = s.UpdatedDate,
                HasAbstract = !string.IsNullOrWhiteSpace(s.Abstract) && s.Abstract.Length >= 30,
                HasKeywords = !string.IsNullOrWhiteSpace(s.Keywords),
                HasFile = s.Files != null && s.Files.Any(),
                HasTopic = !string.IsNullOrWhiteSpace(s.Topic) || s.ConferenceTopicId.HasValue,
                HasPresentationType = !string.IsNullOrWhiteSpace(s.PresentationType)
            }).ToList();

            var noAbstractCount = rows.Count(r => !r.HasAbstract);
            var noFileCount = rows.Count(r => !r.HasFile);
            var noKeywordsCount = rows.Count(r => !r.HasKeywords);
            var staleCount = rows.Count(r => r.IsStale);

            if (missingFilter == "abstract") rows = rows.Where(r => !r.HasAbstract).ToList();
            else if (missingFilter == "file") rows = rows.Where(r => !r.HasFile).ToList();
            else if (missingFilter == "keywords") rows = rows.Where(r => !r.HasKeywords).ToList();
            else if (missingFilter == "stale") rows = rows.Where(r => r.IsStale).ToList();

            rows = rows.OrderByDescending(r => r.DaysSinceActivity).ToList();

            int pageSize = 20;
            int filteredCount = rows.Count;
            rows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new IncompleteSubmissionsViewModel
            {
                Slug = slug ?? "",
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title ?? "",
                TotalCount = all.Count,
                NoAbstractCount = noAbstractCount,
                NoFileCount = noFileCount,
                NoKeywordsCount = noKeywordsCount,
                StaleCount = staleCount,
                Items = rows,
                MissingFilter = missingFilter,
                Page = page,
                PageSize = pageSize,
                FilteredCount = filteredCount
            };

            return View("~/Areas/Admin/Views/Submissions/Incomplete.cshtml", model);
        }

        [HttpGet("/Admin/Submissions/Export")]
        [HttpGet("/{slug}/Admin/Submissions/Export")]
        public async Task<IActionResult> Export(
            string? slug = null,
            Guid? conferenceId = null,
            string? search = null,
            string? status = null,
            string? topic = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                return Forbid();
            }

            var query = _context.Submissions
                .AsNoTracking()
                .Include(s => s.Author)
                .Where(x => x.ConferenceId == conference.Id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchText = search.Trim();
                query = query.Where(x =>
                    (x.Title != null && x.Title.Contains(searchText)) ||
                    (x.Author != null && (
                        (x.Author.FirstName != null && x.Author.FirstName.Contains(searchText)) ||
                        (x.Author.LastName != null && x.Author.LastName.Contains(searchText)) ||
                        (x.Author.Email != null && x.Author.Email.Contains(searchText))
                    ))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<SubmissionStatus>(status, out var parsedStatus))
            {
                query = query.Where(x => x.Status == parsedStatus);
            }

            if (!string.IsNullOrWhiteSpace(topic))
            {
                query = query.Where(x => x.Topic == topic);
            }

            var rows = await query
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    AuthorName = x.Author == null ? "" : ((x.Author.FirstName ?? "") + " " + (x.Author.LastName ?? "")).Trim(),
                    AuthorEmail = x.Author != null ? x.Author.Email ?? "" : "",
                    Status = x.Status.ToString(),
                    Topic = x.Topic ?? "",
                    CreatedDate = x.CreatedDate
                })
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,Başlık,Yazar,E-posta,Durum,Konu,Tarih");

            foreach (var r in rows)
            {
                var id = r.Id;
                var title = $"\"{r.Title?.Replace("\"", "\"\"")}\"";
                var author = $"\"{r.AuthorName.Replace("\"", "\"\"")}\"";
                var email = $"\"{r.AuthorEmail.Replace("\"", "\"\"")}\"";
                var st = r.Status;
                var tp = $"\"{r.Topic.Replace("\"", "\"\"")}\"";
                var dt = r.CreatedDate.ToString("yyyy-MM-dd HH:mm");
                sb.AppendLine($"{id},{title},{author},{email},{st},{tp},{dt}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            var fileName = $"bildirileri_{conference.Title?.Replace(" ", "_") ?? "export"}_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        [HttpGet("/Admin/Submissions/Details/{id:guid}")]
        [HttpGet("/{slug}/Admin/Submissions/Details/{id:guid}")]
        public async Task<IActionResult> Details(
            Guid id,
            string? slug = null,
            Guid? conferenceId = null,
            string? returnUrl = null)
        {
            var submission = await GetAccessibleSubmissionAsync(
                id,
                slug,
                conferenceId);

            if (submission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedView",
                    "Bu bildiriyi görüntüleme yetkiniz yok.");

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return Redirect(BuildSubmissionsUrl(slug, conferenceId));
                }

                return RedirectToAction(nameof(SelectConference));
            }

            var targetTenantId = submission.Conference?.TenantId;
            var effectiveSlug = submission.Conference?.Tenant?.Slug ?? slug ?? "";
            var effectiveConferenceId = submission.ConferenceId;

            var referees = await _userManager.GetUsersInRoleAsync("Referee");

            if (targetTenantId.HasValue)
            {
                referees = referees
                    .Where(r =>
                        r.TenantId.HasValue &&
                        r.TenantId.Value == targetTenantId.Value)
                    .ToList();
            }
            else
            {
                referees = referees
                    .Where(r => false)
                    .ToList();
            }

            ViewBag.Referees = referees;
            ViewBag.Reviews = await _reviewService.GetReviewsBySubmissionIdAsync(id);
            ViewBag.Slug = effectiveSlug;
            ViewBag.ConferenceId = effectiveConferenceId;
            ViewBag.ConferenceTitle = submission.Conference?.Title ?? "";

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : BuildSubmissionsUrl(effectiveSlug, effectiveConferenceId);

            ViewBag.ReturnUrl = effectiveReturnUrl;
            ViewBag.PlagiarismReport = await _plagiarism.GetLatestReportAsync(id);
            ViewBag.PlagiarismConfigured = _plagiarism.IsConfigured;
            ViewBag.DoiMetadata = _doiService.BuildMetadataPreview(submission);
            ViewBag.DoiRegistrationConfigured = _doiRegistration.IsConfigured;

            return View("~/Areas/Admin/Views/Submissions/Details.cshtml", submission);
        }

        [HttpPost("/{slug}/Admin/Submissions/CheckPlagiarism/{id:guid}")]
        [HttpPost("/Admin/Submissions/CheckPlagiarism/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckPlagiarism(string? slug, Guid id, string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await GetAccessibleSubmissionAsync(id, slug, null);
            if (submission?.Files == null)
            {
                submission = await _context.Submissions
                    .Include(s => s.Files)
                    .Include(s => s.Conference)
                    .FirstOrDefaultAsync(s => s.Id == id);
            }

            if (submission == null)
            {
                TempData["ErrorMessage"] = T("Msg_BildiriBulunamadi", "Bildiri bulunamadı.");
                return RedirectBack(returnUrl, slug);
            }

            var fullTextFile = submission.Files?
                .Where(f => f.Type == SubmissionFileType.FullText)
                .OrderByDescending(f => f.Version)
                .FirstOrDefault();

            if (fullTextFile == null)
            {
                TempData["ErrorMessage"] = T("Msg_BuBildiriyeAitTamMetinDosyasi", "Bu bildiriye ait tam metin dosyası bulunamadı.");
                return RedirectBack(returnUrl, slug);
            }

            string resolvedPath;
            try
            {
                resolvedPath = Files.PrivateStorage.Resolve(_env, fullTextFile.FilePath);
            }
            catch
            {
                TempData["ErrorMessage"] = T("Msg_DosyaYolunaErisilemedi", "Dosya yoluna erişilemedi.");
                return RedirectBack(returnUrl, slug, id);
            }

            var report = await _plagiarism.SubmitForCheckAsync(
                submission.Id,
                fullTextFile.Id,
                resolvedPath,
                fullTextFile.FileName,
                user.Id,
                submission.TenantId);

            if (report.Status == PlagiarismStatus.Failed)
            {
                TempData["ErrorMessage"] = $"İntihal kontrolü başlatılamadı: {report.ErrorMessage}";
            }
            else
            {
                TempData["SuccessMessage"] = T("Msg_IntihalKontroluBaslatildiSonucHazirOldugunda", "İntihal kontrolü başlatıldı. Sonuç hazır olduğunda bu sayfada gösterilecektir.");
            }

            return RedirectBack(returnUrl, slug, id);
        }

        [HttpPost("/{slug}/Admin/Submissions/RefreshPlagiarism/{reportId:guid}")]
        [HttpPost("/Admin/Submissions/RefreshPlagiarism/{reportId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshPlagiarism(string? slug, Guid reportId, string? returnUrl)
        {
            var report = await _plagiarism.RefreshStatusAsync(reportId);

            if (report == null)
            {
                TempData["ErrorMessage"] = T("Msg_RaporBulunamadi", "Rapor bulunamadı.");
                return RedirectBack(returnUrl, slug);
            }

            if (report.Status == PlagiarismStatus.Completed)
            {
                TempData["SuccessMessage"] = $"İntihal raporu tamamlandı. Benzerlik oranı: %{report.SimilarityScore}";
            }
            else if (report.Status == PlagiarismStatus.Processing)
            {
                TempData["InfoMessage"] = T("Msg_RaporHenuzHazirDegilLutfenBirkac", "Rapor henüz hazır değil. Lütfen birkaç dakika sonra tekrar deneyin.");
            }
            else
            {
                TempData["ErrorMessage"] = $"Rapor hatası: {report.ErrorMessage}";
            }

            return RedirectBack(returnUrl, slug, report.SubmissionId);
        }

        [HttpPost("/{slug}/Admin/Submissions/SetDoi/{id:guid}")]
        [HttpPost("/Admin/Submissions/SetDoi/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDoi(string? slug, Guid id, string doiUrl, string? returnUrl)
        {
            var submission = await GetAccessibleSubmissionAsync(id, slug, null);
            if (submission == null)
            {
                TempData["ErrorMessage"] = T("Msg_BildiriBulunamadi", "Bildiri bulunamadı.");
                return RedirectBack(returnUrl, slug);
            }

            if (submission.Status != SubmissionStatus.Accepted && submission.Status != SubmissionStatus.Presented)
            {
                TempData["ErrorMessage"] = T("Msg_DOIYalnizcaKabulEdilmisBildirilerIcin", "DOI yalnızca kabul edilmiş bildiriler için atanabilir.");
                return RedirectBack(returnUrl, slug, id);
            }

            if (string.IsNullOrWhiteSpace(doiUrl) || !Uri.TryCreate(doiUrl, UriKind.Absolute, out _))
            {
                TempData["ErrorMessage"] = T("Msg_GecerliBirDOIURLSiGiriniz", "Geçerli bir DOI URL'si giriniz.");
                return RedirectBack(returnUrl, slug, id);
            }

            var tracked = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == id);
            if (tracked != null)
            {
                tracked.DoiUrl = doiUrl.Trim();
                tracked.DoiStatus = DoiStatus.Assigned;
                tracked.DoiProvider = "Manual";
                tracked.DoiAssignedAt = DateTime.UtcNow;
                tracked.DoiErrorMessage = null;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = T("Msg_DOIBasariylaAtandi", "DOI başarıyla atandı.");
            }

            return RedirectBack(returnUrl, slug, id);
        }

        [HttpPost("/{slug}/Admin/Submissions/PrepareDoi/{id:guid}")]
        [HttpPost("/Admin/Submissions/PrepareDoi/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrepareDoi(string? slug, Guid id, string? returnUrl)
        {
            var submission = await GetAccessibleSubmissionAsync(id, slug, null);
            if (submission == null)
            {
                TempData["ErrorMessage"] = T("Msg_BildiriBulunamadi", "Bildiri bulunamadı.");
                return RedirectBack(returnUrl, slug);
            }

            var result = await _doiService.PrepareAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else if (result.Status == DoiStatus.ConfigMissing)
            {
                TempData["InfoMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectBack(returnUrl, slug, id);
        }

        [HttpPost("/{slug}/Admin/Submissions/RegisterDoi/{id:guid}")]
        [HttpPost("/Admin/Submissions/RegisterDoi/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterDoi(string? slug, Guid id, string? returnUrl)
        {
            if (!_doiRegistration.IsConfigured)
            {
                TempData["ErrorMessage"] = T("Msg_DataCiteServisiYapilandirilmamis", "DataCite servisi yapılandırılmamış.");
                return RedirectBack(returnUrl, slug, id);
            }

            var submission = await GetAccessibleSubmissionAsync(id, slug, null);
            if (submission == null)
            {
                TempData["ErrorMessage"] = T("Msg_BildiriBulunamadi", "Bildiri bulunamadı.");
                return RedirectBack(returnUrl, slug);
            }

            if (submission.Status != SubmissionStatus.Accepted && submission.Status != SubmissionStatus.Presented)
            {
                TempData["ErrorMessage"] = T("Msg_DOIYalnizcaKabulEdilmisBildirilerIcin2", "DOI yalnızca kabul edilmiş bildiriler için kaydedilebilir.");
                return RedirectBack(returnUrl, slug, id);
            }

            if (!string.IsNullOrWhiteSpace(submission.DoiUrl))
            {
                TempData["InfoMessage"] = $"Bu bildiriye zaten DOI atanmış: {submission.DoiUrl}";
                return RedirectBack(returnUrl, slug, id);
            }

            var conferenceSlug = submission.Conference?.Tenant?.Slug
                                 ?? submission.Conference?.Slug ?? slug ?? "";

            var submissionCode = !string.IsNullOrWhiteSpace(submission.SubmissionIdCode)
                ? submission.SubmissionIdCode
                : id.ToString("N")[..8].ToUpperInvariant();

            var authors = submission.SubmissionAuthors?
                .OrderBy(a => a.Order)
                .Select(a => new AntAbstract.Infrastructure.Services.Doi.DoiRegistrationAuthor
                {
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    Institution = a.Institution
                })
                .ToList() ?? new();

            var result = await _doiRegistration.RegisterAsync(
                new AntAbstract.Infrastructure.Services.Doi.DoiRegistrationRequest
                {
                    SubmissionId = id,
                    SubmissionCode = submissionCode,
                    Slug = conferenceSlug,
                    Title = submission.Title,
                    Authors = authors,
                    ConferenceTitle = submission.Conference?.Title ?? "",
                    Year = submission.Conference?.StartDate.Year ?? DateTime.UtcNow.Year,
                    Abstract = submission.Abstract
                });

            if (result.Success && !string.IsNullOrWhiteSpace(result.DoiUrl))
            {
                var tracked = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == id);
                if (tracked != null)
                {
                    tracked.DoiUrl = result.DoiUrl;
                    tracked.DoiStatus = Domain.Entities.DoiStatus.Assigned;
                    tracked.DoiProvider = "DataCite";
                    tracked.DoiAssignedAt = DateTime.UtcNow;
                    tracked.DoiErrorMessage = null;
                    await _context.SaveChangesAsync();
                }
                TempData["SuccessMessage"] = $"DOI başarıyla kaydedildi: {result.DoiUrl}";
            }
            else
            {
                TempData["ErrorMessage"] = $"DOI kaydı başarısız: {result.Error}";
            }

            return RedirectBack(returnUrl, slug, id);
        }

        private IActionResult RedirectBack(string? returnUrl, string? slug, Guid? submissionId = null)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (submissionId.HasValue)
            {
                var detailsUrl = string.IsNullOrWhiteSpace(slug)
                    ? $"/Admin/Submissions/Details/{submissionId}"
                    : $"/{slug}/Admin/Submissions/Details/{submissionId}";
                return Redirect(detailsUrl);
            }

            return Redirect(string.IsNullOrWhiteSpace(slug)
                ? "/Admin/Submissions"
                : $"/{slug}/Admin/Submissions");
        }

        [HttpPost("/Admin/Submissions/ChangeStatus")]
        [HttpPost("/{slug}/Admin/Submissions/ChangeStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(
            Guid id,
            string status,
            string? slug = null,
            Guid? conferenceId = null,
            string? returnUrl = null,
            string? adminNote = null)
        {
            var accessibleSubmission = await GetAccessibleSubmissionAsync(
                id,
                slug,
                conferenceId);

            if (accessibleSubmission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedChangeStatus",
                    "Bu bildirinin durumunu değiştirme yetkiniz yok.");

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return Redirect(BuildSubmissionsUrl(slug, conferenceId));
                }

                return RedirectToAction(nameof(SelectConference));
            }

            if (Enum.TryParse<SubmissionStatus>(status, out var newStatus))
            {
                // Eski statüyü audit için sakla
                var submissionForAudit = await _context.Submissions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                var oldStatus = submissionForAudit?.Status.ToString() ?? "?";

                await _submissionService.UpdateStatusAsync(id, newStatus);

                // Karar notunu kaydet
                if (!string.IsNullOrWhiteSpace(adminNote))
                {
                    var submissionEntity = await _context.Submissions.FindAsync(id);
                    if (submissionEntity != null)
                    {
                        submissionEntity.AdminDecisionNote = adminNote.Trim();
                        await _context.SaveChangesAsync();
                    }
                }

                var localizedStatus = GetLocalizedSubmissionStatus(newStatus);
                TempData["SuccessMessage"] = $"Bildiri durumu başarıyla güncellendi: {localizedStatus}";

                // Audit log
                var currentUser = await _userManager.GetUserAsync(User);
                await _audit.LogAsync(
                    category: "Submission",
                    action: "StatusChanged",
                    userId: currentUser?.Id,
                    userName: currentUser != null ? $"{currentUser.FirstName} {currentUser.LastName}".Trim() : null,
                    entityType: "Submission",
                    entityId: id.ToString(),
                    description: $"Durum: {oldStatus} → {newStatus}",
                    conferenceId: accessibleSubmission.ConferenceId,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    oldValues: oldStatus,
                    newValues: newStatus.ToString());

                // Yazara in-app bildirim + e-posta
                try
                {
                    var submission = await _context.Submissions
                        .AsNoTracking()
                        .Include(s => s.Conference)
                        .Include(s => s.Author)
                        .FirstOrDefaultAsync(s => s.Id == id);

                    if (submission?.Author != null)
                    {
                        var (icon, color, statusLabel) = newStatus switch
                        {
                            SubmissionStatus.Accepted => ("✅", "success", "Kabul Edildi"),
                            SubmissionStatus.Rejected => ("❌", "danger", "Reddedildi"),
                            SubmissionStatus.RevisionRequired => ("✏️", "warning", "Revizyon Gerekiyor"),
                            SubmissionStatus.UnderReview => ("🔍", "info", "İncelemede"),
                            SubmissionStatus.Pending => ("⏳", "secondary", "Beklemede"),
                            SubmissionStatus.Presented => ("🎤", "primary", "Sunuldu"),
                            SubmissionStatus.Withdrawn => ("↩️", "dark", "Geri Çekildi"),
                            _ => ("📋", "secondary", localizedStatus)
                        };

                        var conferenceName = submission.Conference?.Title ?? "Kongre";
                        var submissionTitle = submission.Title ?? "Bildiriniz";

                        // In-app bildirim
                        await _notificationService.CreateAsync(
                            userId: submission.AuthorId,
                            title: $"Bildiri Durumu: {statusLabel}",
                            message: $"\"{submissionTitle}\" başlıklı bildirinizin durumu güncellendi: {statusLabel}",
                            icon: icon,
                            color: color,
                            link: $"/Author/Submission/Details/{id}");

                        // E-posta bildirimi
                        if (!string.IsNullOrWhiteSpace(submission.Author.Email))
                        {
                            var fullName = $"{submission.Author.FirstName} {submission.Author.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(fullName)) fullName = submission.Author.Email;

                            var noteHtml = string.IsNullOrWhiteSpace(adminNote)
                                ? ""
                                : $"<p><strong>Yönetici Notu:</strong> {System.Net.WebUtility.HtmlEncode(adminNote)}</p>";

                            await _emailService.SendAsync(
                                submission.Author.Email,
                                $"Bildiri Durumu Güncellendi — {conferenceName}",
                                $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
                                  <div style='background:#1a2d5a;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
                                    <h2 style='margin:0'>{icon} Bildiri Durumu: {statusLabel}</h2>
                                  </div>
                                  <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
                                    <p>Sayın <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p>
                                    <p><strong>{conferenceName}</strong> kongresine gönderdiğiniz
                                       <strong>{System.Net.WebUtility.HtmlEncode(submissionTitle)}</strong>
                                       başlıklı bildirinin durumu güncellendi.</p>
                                    <p><strong>Yeni Durum:</strong> {statusLabel}</p>
                                    {noteHtml}
                                    <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                                  </div>
                                </div>");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Durum değişikliği bildirimi/e-postası gönderilemedi. SubmissionId={Id}", id);
                }
            }
            else
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidStatus",
                    "Geçersiz bildiri durumu.");
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var effectiveSlug = accessibleSubmission.Conference?.Tenant?.Slug ?? slug;
            var effectiveConferenceId = accessibleSubmission.ConferenceId;

            return Redirect(BuildSubmissionsUrl(effectiveSlug, effectiveConferenceId));
        }

        [HttpPost("/Admin/Submissions/Delete")]
        [HttpPost("/{slug}/Admin/Submissions/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            Guid id,
            string? slug = null,
            Guid? conferenceId = null,
            string? returnUrl = null)
        {
            var accessibleSubmission = await GetAccessibleSubmissionAsync(
                id,
                slug,
                conferenceId);

            if (accessibleSubmission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedDelete",
                    "Bu bildiriyi silme yetkiniz yok.");

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return Redirect(BuildSubmissionsUrl(slug, conferenceId));
                }

                return RedirectToAction(nameof(SelectConference));
            }

            await _submissionService.DeleteSubmissionAsync(id);

            TempData["SuccessMessage"] = T(
                "Success_SubmissionDeleted",
                "Bildiri başarıyla silindi.");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var effectiveSlug = accessibleSubmission.Conference?.Tenant?.Slug ?? slug;
            var effectiveConferenceId = accessibleSubmission.ConferenceId;

            return Redirect(BuildSubmissionsUrl(effectiveSlug, effectiveConferenceId));
        }

        private string GetLocalizedSubmissionStatus(SubmissionStatus status)
        {
            return status switch
            {
                SubmissionStatus.New => T("Status_New", "Yeni"),
                SubmissionStatus.Pending => T("Status_Pending", "Beklemede"),
                SubmissionStatus.UnderReview => T("Status_UnderReview", "Hakem Değerlendirmesinde"),
                SubmissionStatus.Accepted => T("Status_Accepted", "Kabul Edildi"),
                SubmissionStatus.Rejected => T("Status_Rejected", "Reddedildi"),
                SubmissionStatus.RevisionRequired => T("Status_RevisionRequired", "Revizyon Gerekli"),
                SubmissionStatus.Presented => T("Status_Presented", "Sunuldu"),
                _ => status.ToString()
            };
        }
    }
}
