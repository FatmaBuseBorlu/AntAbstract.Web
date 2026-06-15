using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.Email;
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            IEmailService emailService)
        {
            _context = context;
            _submissionService = submissionService;
            _reviewService = reviewService;
            _userManager = userManager;
            _tenantAccess = tenantAccess;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _env = env;
            _localizer = localizer;
            _uploadFileValidator = uploadFileValidator;
            _audit = audit;
            _notificationService = notificationService;
            _emailService = emailService;
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

            return View("~/Areas/Admin/Views/Submissions/Create.cshtml", model);
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

                return View("~/Areas/Admin/Views/Submissions/Create.cshtml", model);
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

                    return View("~/Areas/Admin/Views/Submissions/Create.cshtml", model);
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

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
                    FilePath = "/uploads/submissions/" + uniqueFileName,
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
                    ? "Sistemde görüntülenebilecek kongre bulunamadı."
                    : "Kurumunuza bağlı görüntülenebilecek kongre bulunamadı.";
            }

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = IsSuperAdminUser()
                    ? "SuperAdmin olarak sistemdeki tüm kongreleri görebilirsiniz. Başvurularını incelemek istediğiniz kongreyi seçiniz."
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
            string? status = null)
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

            var items = await query
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new AdminSubmissionRowModel
                {
                    Id = x.Id,
                    Title = x.Title ?? "",
                    AuthorName = x.Author == null
                        ? ""
                        : ((x.Author.FirstName ?? "") + " " + (x.Author.LastName ?? "")).Trim(),
                    ConferenceTitle = x.Conference == null ? "" : (x.Conference.Title ?? ""),
                    CreatedAt = x.CreatedDate,
                    Status = x.Status.ToString()
                })
                .ToListAsync();

            var model = new AdminSubmissionsIndexModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                Search = search,
                Status = status,
                Items = items
            };

            return View("~/Areas/Admin/Views/Submissions/Index.cshtml", model);
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

            return View("~/Areas/Admin/Views/Submissions/Details.cshtml", submission);
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
                _ = _audit.LogAsync(
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
                            SubmissionStatus.Accepted        => ("✅", "success", "Kabul Edildi"),
                            SubmissionStatus.Rejected        => ("❌", "danger",  "Reddedildi"),
                            SubmissionStatus.RevisionRequired => ("✏️", "warning", "Revizyon Gerekiyor"),
                            SubmissionStatus.UnderReview     => ("🔍", "info",    "İncelemede"),
                            SubmissionStatus.Pending         => ("⏳", "secondary","Beklemede"),
                            SubmissionStatus.Presented       => ("🎤", "primary", "Sunuldu"),
                            SubmissionStatus.Withdrawn       => ("↩️", "dark",    "Geri Çekildi"),
                            _                                => ("📋", "secondary", localizedStatus)
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
                catch { /* bildirim hatası işlemi durdurmaz */ }
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