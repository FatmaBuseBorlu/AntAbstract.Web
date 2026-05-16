using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Submissions;
using AntAbstract.Web.Models.ViewModels.Shared;
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
    [Authorize(Roles = "Admin")]
    public class SubmissionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISubmissionService _submissionService;
        private readonly IReviewService _reviewService;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SubmissionsController> _localizer;

        public SubmissionsController(
            AppDbContext context,
            ISubmissionService submissionService,
            IReviewService reviewService,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IWebHostEnvironment env,
            IStringLocalizer<SubmissionsController> localizer)
        {
            _context = context;
            _submissionService = submissionService;
            _reviewService = reviewService;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _env = env;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            var user = await GetCurrentUserAsync();

            if (user == null || !user.TenantId.HasValue)
            {
                return null;
            }

            return user.TenantId.Value;
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string? slug)
        {
            if (_tenantContext.Current == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(slug))
            {
                return false;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return false;
            }

            return tenantId.Value == _tenantContext.Current.Id;
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!tenantId.HasValue)
            {
                return query.Where(c => false);
            }

            return query.Where(c => c.TenantId == tenantId.Value);
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

            var query = await GetAccessibleConferenceQueryAsync();

            if (!string.IsNullOrWhiteSpace(slug))
            {
                if (_tenantContext.Current == null ||
                    !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
                    !await CanAccessCurrentTenantAsync(slug))
                {
                    return null;
                }

                query = query.Where(c => c.TenantId == _tenantContext.Current.Id);
            }

            return await query
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);
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

        private async Task<bool> CanAccessSubmissionAsync(Guid submissionId)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return false;
            }

            return await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Conference)
                .AnyAsync(s =>
                    s.Id == submissionId &&
                    s.Conference != null &&
                    s.Conference.TenantId == tenantId.Value);
        }

        [HttpGet("/Admin/Submissions/Create")]
        [HttpGet("/{slug}/Admin/Submissions/Create")]
        public async Task<IActionResult> Create(
            string? slug = null,
            Guid? conferenceId = null)
        {
            var model = new SubmissionCreateViewModel();

            await LoadAvailableConferencesAsync(model);

            if (!string.IsNullOrWhiteSpace(slug))
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

            if (!currentUser.TenantId.HasValue)
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
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.SubmissionFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.SubmissionFile.CopyToAsync(fileStream);
                }

                newSubmission.Files.Add(new SubmissionFile
                {
                    FileName = model.SubmissionFile.FileName,
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

            if (!string.IsNullOrWhiteSpace(redirectSlug))
            {
                return RedirectToAction(nameof(Index), new
                {
                    slug = redirectSlug,
                    conferenceId = conference.Id
                });
            }

            return RedirectToAction(nameof(SelectConference));
        }

        [HttpGet("/Admin/Submissions")]
        public async Task<IActionResult> SelectConference(
            Guid? conferenceId = null,
            string? returnUrl = null)
        {
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

                    return RedirectToAction(
                        nameof(Index),
                        new
                        {
                            slug = selectedConference.Tenant.Slug,
                            conferenceId = selectedConference.Id
                        });
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = T("SelectConference_Lead", "Başvuruları yönetmek için önce kongre seçiniz."),
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

            return RedirectToAction(
                nameof(Index),
                new
                {
                    slug = conference.Tenant.Slug,
                    conferenceId = conference.Id
                });
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
            string? returnUrl = null)
        {
            if (!await CanAccessSubmissionAsync(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedView",
                    "Bu bildiriyi görüntüleme yetkiniz yok.");

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return RedirectToAction(nameof(Index), new { slug });
                }

                return RedirectToAction(nameof(SelectConference));
            }

            var submission = await _submissionService.GetSubmissionByIdAsync(id);

            if (submission == null)
            {
                return NotFound();
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            var referees = await _userManager.GetUsersInRoleAsync("Referee");

            if (tenantId.HasValue)
            {
                referees = referees
                    .Where(r =>
                        r.TenantId.HasValue &&
                        r.TenantId.Value == tenantId.Value)
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

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : $"{Request.PathBase}{Request.Path}{Request.QueryString}";

            if (string.IsNullOrWhiteSpace(effectiveReturnUrl))
            {
                effectiveReturnUrl = string.IsNullOrWhiteSpace(slug)
                    ? "/Admin/Submissions"
                    : $"/{slug}/Admin/Submissions";
            }

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
            string? returnUrl = null)
        {
            if (!await CanAccessSubmissionAsync(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedChangeStatus",
                    "Bu bildirinin durumunu değiştirme yetkiniz yok.");

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return RedirectToAction(nameof(Index), new { slug });
                }

                return RedirectToAction(nameof(SelectConference));
            }

            if (Enum.TryParse<SubmissionStatus>(status, out var newStatus))
            {
                await _submissionService.UpdateStatusAsync(id, newStatus);

                var localizedStatus = GetLocalizedSubmissionStatus(newStatus);

                TempData["SuccessMessage"] = T(
                    "Success_SubmissionStatusUpdated",
                    $"Bildiri durumu güncellendi: {localizedStatus}");
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

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction(nameof(Index), new { slug });
            }

            return RedirectToAction(nameof(SelectConference));
        }

        [HttpPost("/Admin/Submissions/Delete")]
        [HttpPost("/{slug}/Admin/Submissions/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            Guid id,
            string? slug = null,
            string? returnUrl = null)
        {
            if (!await CanAccessSubmissionAsync(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedDelete",
                    "Bu bildiriyi silme yetkiniz yok.");

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return RedirectToAction(nameof(Index), new { slug });
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

            if (!string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction(nameof(Index), new { slug });
            }

            return RedirectToAction(nameof(SelectConference));
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