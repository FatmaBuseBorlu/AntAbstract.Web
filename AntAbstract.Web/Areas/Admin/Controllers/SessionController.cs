using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Sessions;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class SessionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IStringLocalizer<SessionController> _localizer;

        public SessionController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            IAdminTenantAccessService tenantAccess,
            IStringLocalizer<SessionController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _tenantAccess = tenantAccess;
            _localizer = localizer;
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

        private async Task<Guid?> GetCurrentAdminTenantIdAsync()
        {
            if (IsSuperAdminUser())
            {
                return null;
            }

            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CanAccessCurrentTenantAsync(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return false;
            }

            return await _tenantAccess.CanAccessCurrentTenantAsync(User, slug);
        }

        private async Task<IQueryable<Conference>> GetAccessibleConferenceQueryAsync()
        {
            var query = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            return query
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();
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

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c => c.Id == selectedConferenceId.Value);

            if (IsSuperAdminUser())
            {
                query = query.Where(c =>
                    c.Tenant != null &&
                    c.Tenant.Slug == slug);
            }
            else
            {
                query = query.Where(c =>
                    _tenantContext.Current != null &&
                    c.TenantId == _tenantContext.Current.Id);
            }

            return await query.FirstOrDefaultAsync();
        }

        private string BuildProgramSessionsUrl(string slug, Guid conferenceId)
        {
            return $"/{slug}/Admin/ConferenceFlow/ProgramSessions?conferenceId={conferenceId}";
        }

        private string BuildSessionIndexUrl(string slug, Guid conferenceId)
        {
            return $"/{slug}/Admin/Session?conferenceId={conferenceId}";
        }

        private string BuildSessionManageUrl(string slug, Guid sessionId, Guid conferenceId)
        {
            return $"/{slug}/Admin/Session/Manage/{sessionId}?conferenceId={conferenceId}";
        }

        private void SetConferenceViewBag(Conference conference, string slug)
        {
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = slug;
        }

        [HttpGet("/Admin/Session")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            if (!IsSuperAdminUser())
            {
                var tenantId = await GetCurrentAdminTenantIdAsync();

                if (!tenantId.HasValue)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_AdminTenantNotFound",
                        "Admin hesabınıza bağlı kurum bulunamadı.");

                    return Redirect("/Dashboard/MyConferences");
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

                    return Redirect(BuildProgramSessionsUrl(
                        selectedConference.Tenant.Slug,
                        selectedConference.Id));
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

            return Redirect(BuildProgramSessionsUrl(
                conference.Tenant.Slug,
                conference.Id));
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
                .ThenBy(s => s.StartTime)
                .ThenBy(s => s.SortOrder)
                .ToListAsync();

            SetConferenceViewBag(conference, slug);

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

            var fallback = BuildProgramSessionsUrl(slug, conference.Id);

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : fallback;

            var vm = new SessionCreateViewModel
            {
                Slug = slug,
                ConferenceId = conference.Id,
                ConferenceTitle = conference.Title,
                SessionDate = DateTime.Today,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                IsActive = true,
                SortOrder = 0,
                ReturnUrl = effectiveReturnUrl
            };

            SetConferenceViewBag(conference, slug);

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

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError(
                    nameof(model.EndTime),
                    "Bitiş saati başlangıç saatinden sonra olmalıdır.");
            }

            if (!ModelState.IsValid)
            {
                SetConferenceViewBag(conference, slug);

                return View(model);
            }

            var entity = new Session
            {
                Id = Guid.NewGuid(),
                ConferenceId = conference.Id,
                Title = model.Title.Trim(),
                TitleEn = string.IsNullOrWhiteSpace(model.TitleEn) ? null : model.TitleEn.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                DescriptionEn = string.IsNullOrWhiteSpace(model.DescriptionEn) ? null : model.DescriptionEn.Trim(),
                SessionDate = model.SessionDate.Date,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim(),
                SpeakerName = string.IsNullOrWhiteSpace(model.SpeakerName) ? null : model.SpeakerName.Trim(),
                PresentationTitle = string.IsNullOrWhiteSpace(model.PresentationTitle) ? null : model.PresentationTitle.Trim(),
                PresentationTitleEn = string.IsNullOrWhiteSpace(model.PresentationTitleEn) ? null : model.PresentationTitleEn.Trim(),
                SortOrder = model.SortOrder,
                IsActive = model.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            _context.Sessions.Add(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_SessionCreated",
                "Oturum başarıyla oluşturuldu.");

            var fallback = BuildProgramSessionsUrl(slug, conference.Id);

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

            SetConferenceViewBag(conference, slug);

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

            ModelState.Remove(nameof(Session.Conference));
            ModelState.Remove(nameof(Session.Submissions));

            if (session.EndTime <= session.StartTime)
            {
                ModelState.AddModelError(
                    nameof(session.EndTime),
                    "Bitiş saati başlangıç saatinden sonra olmalıdır.");
            }

            if (!ModelState.IsValid)
            {
                SetConferenceViewBag(conference, slug);

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
            existingSession.TitleEn = string.IsNullOrWhiteSpace(session.TitleEn) ? null : session.TitleEn.Trim();
            existingSession.Description = string.IsNullOrWhiteSpace(session.Description) ? null : session.Description.Trim();
            existingSession.DescriptionEn = string.IsNullOrWhiteSpace(session.DescriptionEn) ? null : session.DescriptionEn.Trim();
            existingSession.SessionDate = session.SessionDate.Date;
            existingSession.StartTime = session.StartTime;
            existingSession.EndTime = session.EndTime;
            existingSession.Location = string.IsNullOrWhiteSpace(session.Location) ? null : session.Location.Trim();
            existingSession.SpeakerName = string.IsNullOrWhiteSpace(session.SpeakerName) ? null : session.SpeakerName.Trim();
            existingSession.PresentationTitle = string.IsNullOrWhiteSpace(session.PresentationTitle) ? null : session.PresentationTitle.Trim();
            existingSession.PresentationTitleEn = string.IsNullOrWhiteSpace(session.PresentationTitleEn) ? null : session.PresentationTitleEn.Trim();
            existingSession.SortOrder = session.SortOrder;
            existingSession.IsActive = session.IsActive;
            existingSession.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_SessionUpdated",
                "Oturum başarıyla güncellendi.");

            return Redirect(BuildProgramSessionsUrl(slug, conference.Id));
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

            SetConferenceViewBag(conference, slug);

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

            if (submission == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionNotFound",
                    "Bildiri bulunamadı.");

                return Redirect(BuildSessionManageUrl(slug, sessionId, conference.Id));
            }

            if (submission.Status != SubmissionStatus.Accepted &&
                submission.Status != SubmissionStatus.Presented)
            {
                TempData["ErrorMessage"] = T(
                    "Error_OnlyAcceptedSubmissionCanBeAdded",
                    "Sadece kabul edilen veya sunuldu durumundaki bildiriler programa eklenebilir.");

                return Redirect(BuildSessionManageUrl(slug, sessionId, conference.Id));
            }

            if (submission.SessionId.HasValue &&
                submission.SessionId.Value != sessionId)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionAlreadyAssignedToAnotherSession",
                    "Bu bildiri zaten başka bir oturuma bağlı. Önce mevcut oturumdan çıkarılmalıdır.");

                return Redirect(BuildSessionManageUrl(slug, sessionId, conference.Id));
            }

            if (submission.SessionId == sessionId)
            {
                TempData["InfoMessage"] = T(
                    "Info_SubmissionAlreadyInSession",
                    "Bu bildiri zaten bu oturumda yer alıyor.");

                return Redirect(BuildSessionManageUrl(slug, sessionId, conference.Id));
            }

            submission.SessionId = sessionId;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_SubmissionAddedToSession",
                "Bildiri oturuma başarıyla eklendi.");

            return Redirect(BuildSessionManageUrl(slug, sessionId, conference.Id));
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

            return Redirect(BuildSessionManageUrl(slug, sessionId, conference.Id));
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

            return Redirect(BuildProgramSessionsUrl(slug, conference.Id));
        }
    }
}