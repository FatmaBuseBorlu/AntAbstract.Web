using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.ProceedingBooks;
using AntAbstract.Web.Models.ViewModels.Admin.ProceedingBooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ProceedingBookController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IProceedingBookPdfService _proceedingBookPdfService;
        private readonly INotificationService _notificationService;

        public ProceedingBookController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            IProceedingBookPdfService proceedingBookPdfService,
            INotificationService notificationService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _proceedingBookPdfService = proceedingBookPdfService;
            _notificationService = notificationService;
        }

        private bool IsSuperAdminUser()
        {
            return User.IsInRole("SuperAdmin");
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

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var query = _context.Conferences
                .Include(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                return query;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                return query.Where(c => false);
            }

            return query.Where(c => c.TenantId == tenantId.Value);
        }

        private async Task<Conference?> GetAccessibleConferenceAsync(string slug, Guid? conferenceId)
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

            if (IsSuperAdminUser())
            {
                return await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.Tenant != null &&
                    c.Tenant.Slug == slug);
            }

            if (_tenantContext.Current == null)
            {
                return null;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return await query.FirstOrDefaultAsync(c =>
                c.Id == selectedConferenceId.Value &&
                c.TenantId == _tenantContext.Current.Id);
        }

        private async Task<IActionResult> RedirectToSelectedConferenceProceedingBookAsync(Guid? conferenceId)
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
                TempData["ErrorMessage"] = "Lütfen önce bir kongre seçin.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query.FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);

            if (conference == null || conference.Tenant == null || string.IsNullOrWhiteSpace(conference.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.";
                return Redirect("/Admin/ConferenceFlow");
            }

            SetConferenceSession(conference);

            return Redirect($"/{conference.Tenant.Slug}/Admin/ProceedingBook?conferenceId={conference.Id}");
        }

        private void SetConferenceSession(Conference conference)
        {
            var slug = conference.Tenant?.Slug ?? conference.Slug ?? "";
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        private ProceedingBookViewModel BuildViewModel(Conference conference, string slug, string? returnUrl = null)
        {
            return new ProceedingBookViewModel
            {
                ConferenceId = conference.Id,
                Slug = slug,
                ConferenceTitle = conference.Title ?? "",
                IsProceedingBookPublished = conference.IsProceedingBookPublished,
                ProceedingBookFilePath = conference.ProceedingBookFilePath,
                ProceedingBookPublishedDate = conference.ProceedingBookPublishedDate,
                ReturnUrl = returnUrl
            };
        }

        private static bool IsPdfFile(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);

            return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> SaveProceedingBookFileAsync(IFormFile file, Guid conferenceId)
        {
            var uploadsRoot = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "uploads",
                "proceeding-books",
                conferenceId.ToString());

            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            var safeFileName = $"proceeding-book-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.pdf";
            var fullPath = Path.Combine(uploadsRoot, safeFileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/proceeding-books/{conferenceId}/{safeFileName}";
        }

        private async Task<string> SaveGeneratedProceedingBookPdfAsync(byte[] pdfBytes, Guid conferenceId)
        {
            var uploadsRoot = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "uploads",
                "proceeding-books",
                conferenceId.ToString());

            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            var safeFileName = $"auto-proceeding-book-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.pdf";
            var fullPath = Path.Combine(uploadsRoot, safeFileName);

            await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes);

            return $"/uploads/proceeding-books/{conferenceId}/{safeFileName}";
        }

        private void DeletePhysicalFileIfExists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var normalizedPath = relativePath.TrimStart('/', '\\');

            var fullPath = Path.Combine(
                _webHostEnvironment.WebRootPath,
                normalizedPath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private async Task CreateProceedingBookPublishedNotificationsAsync(Conference conference)
        {
            if (conference == null || conference.Id == Guid.Empty)
            {
                return;
            }

            var registeredUserIds = await _context.Registrations
                .AsNoTracking()
                .Where(x => x.ConferenceId == conference.Id)
                .Select(x => x.AppUserId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToListAsync();

            var acceptedSubmissionAuthorIds = await _context.Submissions
                .AsNoTracking()
                .Where(x =>
                    x.ConferenceId == conference.Id &&
                    x.Status == SubmissionStatus.Accepted)
                .Select(x => x.AuthorId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToListAsync();

            var acceptedSubmissionAuthorEmails = await _context.Submissions
                .AsNoTracking()
                .Where(x =>
                    x.ConferenceId == conference.Id &&
                    x.Status == SubmissionStatus.Accepted)
                .SelectMany(x => x.SubmissionAuthors)
                .Select(x => x.Email)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToListAsync();

            var acceptedSubmissionAuthorEmailsNormalized = acceptedSubmissionAuthorEmails
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToLower())
                .Distinct()
                .ToList();

            var userIdsMatchedByEmail = new List<string>();

            if (acceptedSubmissionAuthorEmailsNormalized.Any())
            {
                userIdsMatchedByEmail = await _context.Users
                    .AsNoTracking()
                    .Where(x =>
                        x.Email != null &&
                        acceptedSubmissionAuthorEmailsNormalized.Contains(x.Email.ToLower()))
                    .Select(x => x.Id)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToListAsync();
            }

            var targetUserIds = registeredUserIds
                .Concat(acceptedSubmissionAuthorIds)
                .Concat(userIdsMatchedByEmail)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (!targetUserIds.Any())
            {
                return;
            }

            var targetUsers = await _context.Users
                .Where(x => targetUserIds.Contains(x.Id))
                .ToListAsync();

            if (!targetUsers.Any())
            {
                return;
            }

            var slug = conference.Tenant?.Slug ?? conference.Slug ?? "";

            var link = !string.IsNullOrWhiteSpace(slug)
                ? $"/{slug}/Proceedings/Index"
                : "/Proceedings/Index";

            var title = "Bildiri Kitabı Yayınlandı";
            var message = $"{conference.Title} bildiri kitabı yayınlandı. PDF dosyasını görüntüleyebilir ve indirebilirsiniz.";

            foreach (var user in targetUsers)
            {
                if (user == null || string.IsNullOrWhiteSpace(user.Id))
                {
                    continue;
                }

                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");

                if (isAdmin || isSuperAdmin)
                {
                    continue;
                }

                var unreadAlreadyExists = await _context.Notifications.AnyAsync(x =>
                    x.UserId == user.Id &&
                    x.Title == title &&
                    x.Message == message &&
                    x.Link == link &&
                    !x.IsRead);

                if (unreadAlreadyExists)
                {
                    continue;
                }

                await _notificationService.CreateAsync(
                    userId: user.Id,
                    title: title,
                    message: message,
                    icon: "fas fa-book-open",
                    color: "primary",
                    link: link);
            }
        }

        [HttpGet("/Admin/ProceedingBook")]
        public async Task<IActionResult> IndexRoot(Guid? conferenceId)
        {
            return await RedirectToSelectedConferenceProceedingBookAsync(conferenceId);
        }

        [HttpGet("/{slug}/Admin/ProceedingBook")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId, string? returnUrl = null)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.";
                return Redirect("/Admin/ConferenceFlow");
            }

            SetConferenceSession(conference);

            var model = BuildViewModel(conference, slug, returnUrl);

            return View("~/Areas/Admin/Views/ProceedingBook/Index.cshtml", model);
        }

        [HttpPost("/Admin/ProceedingBook")]
        [HttpPost("/{slug}/Admin/ProceedingBook")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string? slug, ProceedingBookViewModel model)
        {
            if (model.ConferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "Geçersiz kongre bilgisi.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == model.ConferenceId);

            if (conference == null || conference.Tenant == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var currentSlug = !string.IsNullOrWhiteSpace(slug)
                ? slug
                : conference.Tenant.Slug ?? conference.Slug ?? "";

            SetConferenceSession(conference);

            var wasPublishedBefore = conference.IsProceedingBookPublished;

            if (model.ProceedingBookFile != null && model.ProceedingBookFile.Length > 0)
            {
                const long maxFileSize = 50 * 1024 * 1024;

                if (model.ProceedingBookFile.Length > maxFileSize)
                {
                    ModelState.AddModelError(
                        nameof(model.ProceedingBookFile),
                        "PDF dosyası en fazla 50 MB olabilir.");
                }

                if (!IsPdfFile(model.ProceedingBookFile))
                {
                    ModelState.AddModelError(
                        nameof(model.ProceedingBookFile),
                        "Lütfen sadece PDF dosyası yükleyiniz.");
                }
            }

            var hasExistingFile = !string.IsNullOrWhiteSpace(conference.ProceedingBookFilePath);
            var hasNewFile = model.ProceedingBookFile != null && model.ProceedingBookFile.Length > 0;

            if (model.IsProceedingBookPublished && !hasExistingFile && !hasNewFile)
            {
                ModelState.AddModelError(
                    nameof(model.ProceedingBookFile),
                    "Bildiri kitabını yayına almak için önce PDF dosyası yüklemelisiniz.");
            }

            if (!ModelState.IsValid)
            {
                model.Slug = currentSlug;
                model.ConferenceTitle = conference.Title ?? "";
                model.ProceedingBookFilePath = conference.ProceedingBookFilePath;
                model.ProceedingBookPublishedDate = conference.ProceedingBookPublishedDate;

                return View("~/Areas/Admin/Views/ProceedingBook/Index.cshtml", model);
            }

            if (hasNewFile && model.ProceedingBookFile != null)
            {
                DeletePhysicalFileIfExists(conference.ProceedingBookFilePath);

                var newFilePath = await SaveProceedingBookFileAsync(
                    model.ProceedingBookFile,
                    conference.Id);

                conference.ProceedingBookFilePath = newFilePath;
            }

            conference.IsProceedingBookPublished = model.IsProceedingBookPublished;

            if (conference.IsProceedingBookPublished && !string.IsNullOrWhiteSpace(conference.ProceedingBookFilePath))
            {
                conference.ProceedingBookPublishedDate ??= DateTime.UtcNow;

                if (!wasPublishedBefore)
                {
                    await CreateProceedingBookPublishedNotificationsAsync(conference);
                }
            }
            else
            {
                conference.ProceedingBookPublishedDate = null;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bildiri kitabı ayarları başarıyla kaydedildi.";

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return Redirect($"/{currentSlug}/Admin/ProceedingBook?conferenceId={conference.Id}");
        }

        [HttpPost("/Admin/ProceedingBook/Generate")]
        [HttpPost("/{slug}/Admin/ProceedingBook/Generate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(string? slug, Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "Geçersiz kongre bilgisi.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null || conference.Tenant == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var currentSlug = !string.IsNullOrWhiteSpace(slug)
                ? slug
                : conference.Tenant.Slug ?? conference.Slug ?? "";

            SetConferenceSession(conference);

            var acceptedSubmissions = await _context.Submissions
                .AsNoTracking()
                .Include(x => x.Author)
                .Include(x => x.ConferenceTopic)
                .Where(x =>
                    x.ConferenceId == conference.Id &&
                    x.Status == SubmissionStatus.Accepted)
                .OrderBy(x => x.Topic)
                .ThenBy(x => x.Title)
                .ToListAsync();

            if (!acceptedSubmissions.Any())
            {
                TempData["ErrorMessage"] = "Otomatik bildiri kitabı oluşturmak için kabul edilmiş en az bir bildiri olmalıdır.";
                return Redirect($"/{currentSlug}/Admin/ProceedingBook?conferenceId={conference.Id}");
            }

            try
            {
                var pdfBytes = _proceedingBookPdfService.GenerateProceedingBookPdf(
                    conference,
                    acceptedSubmissions);

                DeletePhysicalFileIfExists(conference.ProceedingBookFilePath);

                var filePath = await SaveGeneratedProceedingBookPdfAsync(
                    pdfBytes,
                    conference.Id);

                conference.ProceedingBookFilePath = filePath;
                conference.IsProceedingBookPublished = true;
                conference.ProceedingBookPublishedDate = DateTime.UtcNow;

                await CreateProceedingBookPublishedNotificationsAsync(conference);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Bildiri kitabı otomatik oluşturuldu ve yayına alındı. Toplam {acceptedSubmissions.Count} kabul edilmiş bildiri eklendi.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bildiri kitabı oluşturulurken bir hata oluştu: {ex.Message}";
            }

            return Redirect($"/{currentSlug}/Admin/ProceedingBook?conferenceId={conference.Id}");
        }

        [HttpPost("/Admin/ProceedingBook/RemoveFile")]
        [HttpPost("/{slug}/Admin/ProceedingBook/RemoveFile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFile(string? slug, Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "Geçersiz kongre bilgisi.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conference = await query
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null || conference.Tenant == null)
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.";
                return Redirect("/Admin/ConferenceFlow");
            }

            var currentSlug = !string.IsNullOrWhiteSpace(slug)
                ? slug
                : conference.Tenant.Slug ?? conference.Slug ?? "";

            SetConferenceSession(conference);

            DeletePhysicalFileIfExists(conference.ProceedingBookFilePath);

            conference.ProceedingBookFilePath = null;
            conference.IsProceedingBookPublished = false;
            conference.ProceedingBookPublishedDate = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bildiri kitabı PDF dosyası kaldırıldı.";

            return Redirect($"/{currentSlug}/Admin/ProceedingBook?conferenceId={conference.Id}");
        }
    }
}