using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Sessions;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SessionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<SessionController> _localizer;

        public SessionController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<SessionController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
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

        private async Task<Conference?> GetConferenceOrNull(string? slug, Guid? conferenceId)
        {
            if (!await CanAccessCurrentTenantAsync(slug))
            {
                return null;
            }

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

            return await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value &&
                    c.TenantId == _tenantContext.Current!.Id);
        }

        [HttpGet("/Admin/Session")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
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

                    return Redirect($"/{selectedConference.Tenant.Slug}/Admin/Session?conferenceId={selectedConference.Id}");
                }
            }

            var query = await GetAccessibleConferenceQueryAsync();

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = T("SelectConference_Lead", "Oturumları yönetmek için önce kongre seçiniz."),
                PostUrl = "/Admin/Session/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Session/Select")]
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

            return Redirect($"/{conference.Tenant.Slug}/Admin/Session?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/Session")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var sessions = await _context.Sessions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Submissions)
                .OrderBy(s => s.SessionDate)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;
            ViewBag.Slug = slug;

            return View(sessions);
        }

        [HttpGet("/{slug}/Admin/Session/Create")]
        public async Task<IActionResult> Create(
            string slug,
            Guid? conferenceId,
            string? returnUrl = null)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var fallback = $"/{slug}/Admin/Session?conferenceId={conference.Id}";

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : fallback;

            var vm = new SessionCreateViewModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                SessionDate = DateTime.Now,
                ReturnUrl = effectiveReturnUrl
            };

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;
            ViewBag.Slug = slug;

            return View(vm);
        }

        [HttpPost("/{slug}/Admin/Session/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string slug,
            SessionCreateViewModel model,
            Guid? conferenceId)
        {
            var effectiveConferenceId = model.ConferenceId != Guid.Empty
                ? model.ConferenceId
                : conferenceId;

            var conference = await GetConferenceOrNull(slug, effectiveConferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            model.Slug = slug;
            model.ConferenceId = conference.Id;
            model.ConferenceTitle = conference.Title;

            if (!ModelState.IsValid)
            {
                ViewBag.ConferenceId = conference.Id;
                ViewBag.ConferenceName = conference.Title;
                ViewBag.Slug = slug;

                return View(model);
            }

            var entity = new Session
            {
                Id = Guid.NewGuid(),
                ConferenceId = conference.Id,
                Title = (model.Title ?? "").Trim(),
                SessionDate = model.SessionDate,
                Location = string.IsNullOrWhiteSpace(model.Location)
                    ? null
                    : model.Location.Trim()
            };

            _context.Sessions.Add(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_SessionCreated",
                "Oturum başarıyla oluşturuldu.");

            var fallback = $"/{slug}/Admin/Session?conferenceId={conference.Id}";

            var go = !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? model.ReturnUrl
                : fallback;

            return Redirect(go);
        }

        [HttpGet("/{slug}/Admin/Session/Edit/{id:guid}")]
        public async Task<IActionResult> Edit(
            string slug,
            Guid id,
            Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var session = await _context.Sessions
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.ConferenceId == conference.Id);

            if (session == null)
            {
                return NotFound();
            }

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;
            ViewBag.Slug = slug;

            return View(session);
        }

        [HttpPost("/{slug}/Admin/Session/Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string slug,
            Guid id,
            Session session,
            Guid? conferenceId)
        {
            if (id != session.Id)
            {
                return NotFound();
            }

            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            if (!ModelState.IsValid)
            {
                ViewBag.ConferenceId = conference.Id;
                ViewBag.ConferenceName = conference.Title;
                ViewBag.Slug = slug;

                return View(session);
            }

            var existingSession = await _context.Sessions
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.ConferenceId == conference.Id);

            if (existingSession == null)
            {
                return NotFound();
            }

            existingSession.Title = (session.Title ?? "").Trim();
            existingSession.Location = string.IsNullOrWhiteSpace(session.Location)
                ? null
                : session.Location.Trim();
            existingSession.SessionDate = session.SessionDate;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_SessionUpdated",
                "Oturum başarıyla güncellendi.");

            return Redirect($"/{slug}/Admin/Session?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Admin/Session/Manage/{id:guid}")]
        public async Task<IActionResult> Manage(
            string slug,
            Guid id,
            Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var session = await _context.Sessions
                .Include(s => s.Submissions)
                    .ThenInclude(sub => sub.Author)
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.ConferenceId == conference.Id);

            if (session == null)
            {
                return NotFound();
            }

            var unassignedSubmissions = await _context.Submissions
                .AsNoTracking()
                .Where(s =>
                    s.ConferenceId == conference.Id &&
                    (
                        s.Status == SubmissionStatus.Accepted ||
                        s.Status == SubmissionStatus.Presented
                    ) &&
                    s.SessionId == null)
                .Include(s => s.Author)
                .OrderBy(s => s.Title)
                .ToListAsync();

            ViewBag.UnassignedSubmissions = unassignedSubmissions;
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;
            ViewBag.Slug = slug;

            return View(session);
        }

        [HttpPost("/{slug}/Admin/Session/AddSubmission")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubmission(
            string slug,
            Guid sessionId,
            Guid submissionId,
            Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var session = await _context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == sessionId &&
                    s.ConferenceId == conference.Id);

            if (session == null)
            {
                return NotFound();
            }

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s =>
                    s.Id == submissionId &&
                    s.ConferenceId == conference.Id);

            if (submission != null)
            {
                submission.SessionId = sessionId;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = T(
                    "Success_SubmissionAddedToSession",
                    "Bildiri oturuma başarıyla eklendi.");
            }

            return Redirect($"/{slug}/Admin/Session/Manage/{sessionId}?conferenceId={conference.Id}");
        }

        [HttpPost("/{slug}/Admin/Session/RemoveSubmission")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSubmission(
            string slug,
            Guid sessionId,
            Guid submissionId,
            Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var session = await _context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == sessionId &&
                    s.ConferenceId == conference.Id);

            if (session == null)
            {
                return NotFound();
            }

            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s =>
                    s.Id == submissionId &&
                    s.ConferenceId == conference.Id);

            if (submission != null && submission.SessionId == sessionId)
            {
                submission.SessionId = null;

                await _context.SaveChangesAsync();

                TempData["InfoMessage"] = T(
                    "Info_SubmissionRemovedFromSession",
                    "Bildiri oturumdan çıkarıldı.");
            }

            return Redirect($"/{slug}/Admin/Session/Manage/{sessionId}?conferenceId={conference.Id}");
        }

        [HttpPost("/{slug}/Admin/Session/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            string slug,
            Guid id,
            Guid? conferenceId)
        {
            var conference = await GetConferenceOrNull(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(nameof(SelectConference));
            }

            SetSelectedConferenceSession(conference);

            var session = await _context.Sessions
                .Include(s => s.Submissions)
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.ConferenceId == conference.Id);

            if (session != null)
            {
                foreach (var submission in session.Submissions)
                {
                    submission.SessionId = null;
                }

                _context.Sessions.Remove(session);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = T(
                    "Success_SessionDeleted",
                    "Oturum başarıyla silindi.");
            }

            return Redirect($"/{slug}/Admin/Session?conferenceId={conference.Id}");
        }
    }
}