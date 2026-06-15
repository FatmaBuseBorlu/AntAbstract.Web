using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Web.Models.ViewModels.Admin.Decision;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Security;
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
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IStringLocalizer<DecisionController> _localizer;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;

        public DecisionController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IAdminTenantAccessService tenantAccess,
            IStringLocalizer<DecisionController> localizer,
            IEmailService emailService,
            INotificationService notificationService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _tenantAccess = tenantAccess;
            _localizer = localizer;
            _emailService = emailService;
            _notificationService = notificationService;
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
            return await _tenantAccess.GetAdminTenantIdAsync(User);
        }

        private async Task<bool> CurrentAdminHasTenantAsync()
        {
            if (IsSuperAdminUser())
            {
                return true;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            return tenantId.HasValue && tenantId.Value != Guid.Empty;
        }

        private async Task<bool> CanAccessCurrentTenantAsync()
        {
            if (IsSuperAdminUser())
            {
                return true;
            }

            return await _tenantAccess.CanAccessCurrentTenantAsync(
                User,
                allowSuperAdmin: false);
        }

        private static bool SlugMatches(Conference? conference, string? slug)
        {
            if (conference == null || string.IsNullOrWhiteSpace(slug))
            {
                return false;
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

        private static string GetCanonicalSlug(Conference? conference, string? fallbackSlug = null)
        {
            return conference?.Tenant?.Slug
                   ?? conference?.Slug
                   ?? fallbackSlug
                   ?? "";
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

        private Guid? GetSelectedConferenceId(Guid? tenantId = null)
        {
            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();

            if (selectedConferenceId.HasValue && selectedConferenceId.Value != Guid.Empty)
            {
                return selectedConferenceId.Value;
            }

            return GetSelectedConferenceIdFromSession(tenantId);
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
            string slug,
            Guid? conferenceId)
        {
            Guid? selectedConferenceId;

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                selectedConferenceId = conferenceId.Value;
            }
            else
            {
                selectedConferenceId = GetSelectedConferenceId(_tenantContext.Current?.Id);
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
                var conference = await query.FirstOrDefaultAsync(c =>
                    c.Id == selectedConferenceId.Value);

                if (conference == null || !SlugMatches(conference, slug))
                {
                    return null;
                }

                return conference;
            }

            if (_tenantContext.Current == null)
            {
                return null;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!await CanAccessCurrentTenantAsync())
            {
                return null;
            }

            return await query.FirstOrDefaultAsync(c =>
                c.Id == selectedConferenceId.Value &&
                c.TenantId == _tenantContext.Current.Id);
        }

        private void SetSelectedConferenceSession(Conference conference)
        {
            var slug = GetCanonicalSlug(conference, _tenantContext.Current?.Slug);
            var tenantId = conference.TenantId;

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{tenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{tenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{tenantId}", conference.Title ?? "");
        }

        [HttpPost("/{slug}/Admin/Decision/BulkDecision")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDecision(
            string slug,
            Guid[] submissionIds,
            string decision)
        {
            if (submissionIds == null || submissionIds.Length == 0)
            {
                TempData["ErrorMessage"] = T("Error_NoBulkSelection", "Lütfen işlem yapılacak bildiri seçin.");
                return Redirect($"/{slug}/Admin/Decision");
            }

            var validDecisions = new[] { "Accept", "Reject", "Revision" };
            if (!validDecisions.Contains(decision))
            {
                TempData["ErrorMessage"] = T("Error_InvalidDecision", "Geçersiz karar seçimi.");
                return Redirect($"/{slug}/Admin/Decision");
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));

            if (conference == null)
            {
                TempData["ErrorMessage"] = T("Error_ConferenceNotFound", "Kongre bulunamadı.");
                return Redirect($"/{slug}/Admin/Decision");
            }

            var submissions = await _context.Submissions
                .Where(s =>
                    submissionIds.Contains(s.Id) &&
                    s.ConferenceId == conference.Id &&
                    (s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.UnderReview))
                .ToListAsync();

            if (submissions.Count == 0)
            {
                TempData["ErrorMessage"] = T("Error_NoBulkSelection", "Seçilen bildirilerde işlem yapılabilecek kayıt bulunamadı.");
                return Redirect($"/{slug}/Admin/Decision");
            }

            var newStatus = decision switch
            {
                "Accept"   => SubmissionStatus.Accepted,
                "Reject"   => SubmissionStatus.Rejected,
                "Revision" => SubmissionStatus.RevisionRequired,
                _          => SubmissionStatus.Pending
            };

            var now = DateTime.UtcNow;

            foreach (var s in submissions)
            {
                s.Status = newStatus;
                s.DecisionDate = now;
                s.UpdatedDate = now;
            }

            await _context.SaveChangesAsync();

            // Her yazara email gönder (arka planda, hata olsa bile devam et)
            foreach (var s in submissions)
            {
                await SendDecisionEmailAsync(s, conference, decision, null);
            }

            var decisionLabel = decision switch
            {
                "Accept"   => "kabul edildi",
                "Reject"   => "reddedildi",
                "Revision" => "revizyon istendi",
                _          => "güncellendi"
            };

            TempData["SuccessMessage"] = $"{submissions.Count} bildiri {decisionLabel}.";

            return Redirect($"/{slug}/Admin/Decision");
        }

        private async Task SendDecisionEmailAsync(
            Submission submission,
            Conference conference,
            string decision,
            string? note)
        {
            try
            {
                var author = await _userManager.FindByIdAsync(submission.AuthorId ?? "");

                if (author == null || string.IsNullOrWhiteSpace(author.Email))
                {
                    return;
                }

                var fullName = $"{author.FirstName} {author.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = author.UserName ?? author.Email;
                }

                var (subject, statusLabel, badgeColor, statusMessage) = decision switch
                {
                    "Accept" => (
                        $"Bildiriniz Kabul Edildi — {conference.Title}",
                        "Kabul Edildi",
                        "#28a745",
                        "Bildiriniz kongre bilim kurulu tarafından değerlendirilmiş ve <strong>kabul edilmiştir</strong>. Tebrikler!"
                    ),
                    "Reject" => (
                        $"Bildiriniz Hakkında Karar — {conference.Title}",
                        "Reddedildi",
                        "#dc3545",
                        "Bildiriniz kongre bilim kurulu tarafından değerlendirilmiş, ancak bu aşamada programa alınamamıştır."
                    ),
                    "Revision" => (
                        $"Bildiriniz İçin Revizyon İstendi — {conference.Title}",
                        "Revizyon Gerekli",
                        "#fd7e14",
                        "Bildiriniz kongre bilim kurulu tarafından değerlendirilmiş ve bazı düzeltmeler yapılması istenmiştir. Lütfen aşağıdaki notları inceleyerek bildirinizi güncelleyiniz."
                    ),
                    _ => (
                        $"Bildiriniz Hakkında Bilgi — {conference.Title}",
                        "Değerlendirildi",
                        "#6c757d",
                        "Bildiriniz değerlendirilmiştir."
                    )
                };

                var noteHtml = !string.IsNullOrWhiteSpace(note)
                    ? $@"<div style='background:#f8f9fa;border-left:4px solid #6c757d;padding:12px 16px;margin:16px 0;border-radius:4px;'>
                           <p style='margin:0 0 4px 0;font-weight:600;color:#495057;'>Değerlendirici Notu:</p>
                           <p style='margin:0;color:#495057;'>{System.Net.WebUtility.HtmlEncode(note)}</p>
                         </div>"
                    : "";

                // Önce DB şablonunu dene; bulamazsa sabit HTML ile gönder
                var templateKey = decision switch
                {
                    "Accept"   => "decision.accept",
                    "Reject"   => "decision.reject",
                    "Revision" => "decision.revision",
                    _          => ""
                };

                if (!string.IsNullOrEmpty(templateKey))
                {
                    await _emailService.SendTemplatedAsync(author.Email, templateKey, new Dictionary<string, string>
                    {
                        { "{FullName}",         fullName },
                        { "{ConferenceTitle}",  conference.Title ?? "" },
                        { "{SubmissionTitle}",  submission.Title ?? "" },
                        { "{StatusLabel}",      statusLabel },
                        { "{Note}",             noteHtml }
                    });
                }
                else
                {
                    var html = $@"
<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'></head>
<body style='font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:20px;'>
  <div style='max-width:600px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;'>
    <div style='background:{badgeColor};padding:24px 32px;'>
      <h1 style='color:#fff;margin:0;font-size:22px;'>Bildiri Kararı</h1>
    </div>
    <div style='padding:32px;'>
      <p>Sayın <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p>
      <p>{statusMessage}</p>
      <p><strong>Bildiri:</strong> {System.Net.WebUtility.HtmlEncode(submission.Title ?? "")}</p>
      {noteHtml}
    </div>
  </div>
</body></html>";
                    await _emailService.SendAsync(author.Email, subject, html);
                }
            }
            catch
            {
                // Email gönderilemese bile işlem başarısız sayılmaz — sadece loglanır
            }
        }

        private string BuildDecisionUrl(string slug, Guid conferenceId)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return $"/Admin/Decision?conferenceId={conferenceId}";
            }

            return $"/{slug}/Admin/Decision?conferenceId={conferenceId}";
        }

        private async Task<Submission?> GetAccessibleSubmissionForDecisionAsync(
            string slug,
            Guid submissionId)
        {
            var query = _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.Conference)
                    .ThenInclude(c => c.Tenant)
                .AsQueryable();

            if (IsSuperAdminUser())
            {
                var submission = await query.FirstOrDefaultAsync(s =>
                    s.Id == submissionId &&
                    s.Conference != null);

                if (submission == null || !SlugMatches(submission.Conference, slug))
                {
                    return null;
                }

                return submission;
            }

            if (_tenantContext.Current == null)
            {
                return null;
            }

            if (!string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var tenantId = await GetCurrentAdminTenantIdAsync();

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return null;
            }

            return await query.FirstOrDefaultAsync(s =>
                s.Id == submissionId &&
                s.Conference != null &&
                s.Conference.TenantId == tenantId.Value &&
                s.Conference.Tenant != null &&
                s.Conference.Tenant.Slug == slug);
        }

        [HttpGet("/Admin/Decision")]
        public async Task<IActionResult> SelectConference(string? returnUrl = null)
        {
            if (!await CurrentAdminHasTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            var selectedId = GetSelectedConferenceId(_tenantContext.Current?.Id);

            if (selectedId.HasValue && selectedId.Value != Guid.Empty)
            {
                var selectedQuery = await GetAccessibleConferenceQueryAsync();

                var selectedConference = await selectedQuery
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (selectedConference?.Tenant != null &&
                    !string.IsNullOrWhiteSpace(GetCanonicalSlug(selectedConference)))
                {
                    SetSelectedConferenceSession(selectedConference);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    return Redirect(BuildDecisionUrl(
                        GetCanonicalSlug(selectedConference),
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
                    ? T("Error_NoConferenceForSuperAdmin", "Sistemde görüntülenebilecek kongre bulunamadı.")
                    : T("Error_NoConferenceForAdmin", "Kurumunuza bağlı görüntülenebilecek kongre bulunamadı.");
            }

            var vm = new SelectConferenceViewModel
            {
                Title = T("SelectConference_Title", "Kongre Seç"),
                Lead = IsSuperAdminUser()
                    ? T("SelectConference_SuperAdminLead", "SuperAdmin olarak sistemdeki tüm kongreleri görebilirsiniz. Karar ekranını incelemek istediğiniz kongreyi seçiniz.")
                    : T("SelectConference_Lead", "Karar ekranını görüntülemek için önce kongre seçiniz."),
                PostUrl = "/Admin/Decision/Select",
                SubmitText = T("SelectConference_Submit", "Devam Et"),
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Decision/Select")]
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

            var canonicalSlug = GetCanonicalSlug(conference);

            if (conference == null || string.IsNullOrWhiteSpace(canonicalSlug))
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

            return Redirect(BuildDecisionUrl(canonicalSlug, conference.Id));
        }

        [HttpGet("/{slug}/Admin/Decision")]
        public async Task<IActionResult> Index(
            string slug,
            Guid? conferenceId)
        {
            var conference = await GetAccessibleConferenceAsync(slug, conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SelectConferenceFirst",
                    "Lütfen yetkili olduğunuz geçerli bir kongre seçiniz.");

                return RedirectToAction(
                    nameof(SelectConference),
                    new { returnUrl = $"/{slug}/Admin/Decision" });
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(BuildDecisionUrl(canonicalSlug, conference.Id));
            }

            SetSelectedConferenceSession(conference);

            var allSubmissions = _context.Submissions
                .AsNoTracking()
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Reviewer)
                .Include(s => s.ReviewAssignments)
                    .ThenInclude(ra => ra.Review)
                .OrderByDescending(s => s.CreatedDate)
                .AsQueryable();

            var awaitingDecision = await allSubmissions
                .Where(s =>
                    s.Status == SubmissionStatus.Pending ||
                    s.Status == SubmissionStatus.UnderReview)
                .ToListAsync();

            var decided = await allSubmissions
                .Where(s =>
                    s.Status == SubmissionStatus.Accepted ||
                    s.Status == SubmissionStatus.Rejected ||
                    s.Status == SubmissionStatus.RevisionRequired)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = canonicalSlug;

            var viewModel = new DecisionIndexViewModel
            {
                AwaitingDecision = awaitingDecision,
                AlreadyDecided = decided
            };

            return View("~/Areas/Admin/Views/Decision/Index.cshtml", viewModel);
        }

        [HttpGet("/Decision/Index")]
        public IActionResult LegacyRoot()
        {
            return Redirect("/Admin/Decision");
        }

        [HttpGet("/{slug}/Decision/Index")]
        public IActionResult LegacyTenant(string slug)
        {
            return Redirect($"/{slug}/Admin/Decision");
        }

        [HttpPost("/{slug}/Admin/Decision/MakeDecision")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeDecision(
            string slug,
            Guid submissionId,
            string decision,
            string? note = null)
        {
            var submission = await GetAccessibleSubmissionForDecisionAsync(
                slug,
                submissionId);

            if (submission == null || submission.Conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_SubmissionNotLinkedToTenantConference",
                    "Bildiri bulunamadı veya bu bildiriye karar verme yetkiniz yok.");

                return Redirect($"/{slug}/Admin/Decision");
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == submission.ConferenceId);

            if (conference == null || !SlugMatches(conference, slug))
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı veya bu kongreye erişim yetkiniz yok.");

                return Redirect("/Admin/Decision");
            }

            if (!IsSuperAdminUser())
            {
                var adminTenantId = await GetCurrentAdminTenantIdAsync();

                if (!adminTenantId.HasValue ||
                    adminTenantId.Value == Guid.Empty ||
                    conference.TenantId != adminTenantId.Value)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_TenantMismatch",
                        "Bu kongre için karar verme yetkiniz yok.");

                    return RedirectToAction(nameof(SelectConference));
                }
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference);

            string decisionText;

            if (decision == "Accept")
            {
                submission.Status = SubmissionStatus.Accepted;
                decisionText = T("Decision_Accepted", "Kabul Edildi");
            }
            else if (decision == "Reject")
            {
                submission.Status = SubmissionStatus.Rejected;
                decisionText = T("Decision_Rejected", "Reddedildi");
            }
            else if (decision == "Revision")
            {
                submission.Status = SubmissionStatus.RevisionRequired;
                decisionText = T("Decision_RevisionRequested", "Revizyon İstendi");
            }
            else
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidDecision",
                    "Geçersiz karar seçimi.");

                return Redirect(BuildDecisionUrl(canonicalSlug, submission.ConferenceId));
            }

            submission.DecisionDate = DateTime.UtcNow;
            submission.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Yazara karar bildirimi emaili gönder
            await SendDecisionEmailAsync(submission, conference, decision, note);

            // Yazara in-app bildirim
            try
            {
                if (!string.IsNullOrWhiteSpace(submission.AuthorId))
                {
                    var (notifIcon, notifColor, notifTitle) = decision switch
                    {
                        "Accept"   => ("✅", "success", "Bildiriniz Kabul Edildi"),
                        "Reject"   => ("❌", "danger",  "Bildiri Değerlendirme Sonucu"),
                        "Revision" => ("🔄", "warning", "Bildiriniz Revizyon Gerektiriyor"),
                        _          => ("📋", "info",    "Bildiri Güncellendi")
                    };

                    await _notificationService.CreateAsync(
                        userId: submission.AuthorId,
                        title: notifTitle,
                        message: $"\"{submission.Title}\" başlıklı bildiriniz hakkında karar açıklandı.",
                        icon: notifIcon,
                        color: notifColor,
                        link: null);
                }
            }
            catch { }

            TempData["SuccessMessage"] = T(
                "Success_SubmissionDecisionSaved",
                $"Bildiri kararı kaydedildi: {decisionText}");

            return Redirect(BuildDecisionUrl(canonicalSlug, submission.ConferenceId));
        }
    }
}