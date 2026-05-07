using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    [Route("{slug}/Payment")]
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;
        private readonly INotificationService _notificationService;
        private readonly IStringLocalizer<PaymentController> _localizer;

        public PaymentController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            INotificationService notificationService,
            IStringLocalizer<PaymentController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _notificationService = notificationService;
            _localizer = localizer;
        }

        #region Helper Methods

        private string GetSlug()
        {
            return RouteData.Values["slug"]?.ToString()
                   ?? _tenantContext.Current?.Slug
                   ?? HttpContext.Session.GetString("SelectedConferenceSlug")
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

        private Guid? GetSelectedConferenceId()
        {
            string? conferenceIdText = null;

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                conferenceIdText = HttpContext.Session.GetString(tenantKey);
            }

            conferenceIdText ??= HttpContext.Session.GetString("SelectedConferenceId");

            return Guid.TryParse(conferenceIdText, out var parsedId)
                ? parsedId
                : null;
        }

        private IActionResult RedirectToConferencePicker(
            string slug,
            string returnUrl,
            string? message = null)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = message;
            }

            var url = string.IsNullOrWhiteSpace(slug)
                ? $"/Dashboard/MyConferences?returnUrl={Uri.EscapeDataString(returnUrl)}"
                : $"/{slug}/Dashboard/MyConferences?returnUrl={Uri.EscapeDataString(returnUrl)}";

            return Redirect(url);
        }

        private static bool SlugMatches(Conference? conference, string slug)
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

        private void SetSelectedConferenceSession(Conference conference, string slug)
        {
            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
        }

        private async Task<Conference?> GetConferenceBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await _context.Conferences
                .Include(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug));
        }

        private async Task<Conference?> GetSelectedConferenceAsync(string slug)
        {
            var selectedConferenceId = GetSelectedConferenceId();

            if (selectedConferenceId.HasValue)
            {
                var selectedConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);

                if (selectedConference != null)
                {
                    return selectedConference;
                }
            }

            return await GetConferenceBySlugAsync(slug);
        }

        private async Task<bool> HasAcceptedSubmissionAsync(string userId, Guid conferenceId)
        {
            return await _context.Submissions
                .AsNoTracking()
                .AnyAsync(s =>
                    s.AuthorId == userId &&
                    s.ConferenceId == conferenceId &&
                    (
                        s.Status == SubmissionStatus.Accepted ||
                        s.Status == SubmissionStatus.Presented
                    ));
        }

        #endregion

        [HttpGet("/Payment/My")]
        public IActionResult MyFromDashboard()
        {
            var selectedSlug = HttpContext.Session.GetString("SelectedConferenceSlug");

            if (!string.IsNullOrWhiteSpace(selectedSlug))
            {
                return Redirect(BuildUrl(selectedSlug, "/payments"));
            }

            return Redirect("/Dashboard/MyConferences");
        }

        [HttpGet("/{slug}/payments")]
        [HttpGet("My")]
        public async Task<IActionResult> My()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var slug = GetSlug();

            var returnUrl = string.IsNullOrWhiteSpace(slug)
                ? "/Payment/My"
                : BuildUrl(slug, "/payments");

            var conference = await GetSelectedConferenceAsync(slug);

            if (conference == null)
            {
                return RedirectToConferencePicker(
                    slug,
                    returnUrl,
                    T("SelectConferenceForPaymentHistory", "Ödeme geçmişini görüntülemek için önce bir kongre seçmelisiniz."));
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference, canonicalSlug);

            if (!string.IsNullOrWhiteSpace(canonicalSlug) &&
                !string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(BuildUrl(canonicalSlug, "/payments"));
            }

            var payments = await _context.Payments
                .Include(p => p.Conference)
                .Where(p =>
                    p.AppUserId == user.Id &&
                    p.ConferenceId == conference.Id)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            return View(payments);
        }

        [HttpGet("/Payment/New")]
        [HttpGet("/{slug}/payment")]
        [HttpGet("New")]
        public async Task<IActionResult> New()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var slug = GetSlug();

            var conference = await GetSelectedConferenceAsync(slug);

            if (conference == null)
            {
                return RedirectToConferencePicker(
                    slug,
                    BuildUrl(slug, "/payment"),
                    T("SelectConferenceBeforePayment", "Ödeme yapmadan önce bir kongre seçmelisiniz."));
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference, canonicalSlug);

            var registration = await _context.Registrations
                .Include(r => r.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r =>
                    r.AppUserId == user.Id &&
                    r.ConferenceId == conference.Id);

            if (registration == null)
            {
                TempData["InfoMessage"] = T(
                    "RegisterBeforePayment",
                    "Ödeme yapmadan önce kongreye kayıt olmalısınız.");

                return Redirect(BuildUrl(canonicalSlug, "/register"));
            }

            if (registration.IsPaid)
            {
                TempData["SuccessMessage"] = T(
                    "PaymentAlreadyCompleted",
                    "Bu kongre için ödemeniz zaten tamamlanmış.");

                return Redirect(BuildUrl(canonicalSlug, "/payments"));
            }

            var hasAcceptedSubmission = await HasAcceptedSubmissionAsync(
                user.Id,
                registration.ConferenceId);

            if (!hasAcceptedSubmission)
            {
                TempData["InfoMessage"] = T(
                    "PaymentRequiresAcceptedSubmission",
                    "Ödeme ekranı, bildiriniz kabul edildikten sonra açılır.");

                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }

            return Redirect(BuildUrl(canonicalSlug, $"/payment/checkout/{registration.Id}"));
        }

        [HttpGet("Index/{id:guid}")]
        [HttpGet("/{slug}/payment/checkout/{id:guid}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var registration = await _context.Registrations
                .Include(r => r.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.AppUserId == user.Id);

            if (registration == null)
            {
                return NotFound(T("RegistrationNotFound", "Kayıt bulunamadı."));
            }

            var slug = GetSlug();
            var canonicalSlug = GetCanonicalSlug(registration.Conference, slug);

            if (!SlugMatches(registration.Conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/payment/checkout/{id}"));
            }

            if (registration.Conference != null)
            {
                SetSelectedConferenceSession(registration.Conference, canonicalSlug);
            }

            if (registration.IsPaid)
            {
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p =>
                        p.AppUserId == user.Id &&
                        p.ConferenceId == registration.ConferenceId &&
                        p.RelatedSubmissionId == registration.Id);

                return RedirectToAction(
                    nameof(Success),
                    new
                    {
                        slug = canonicalSlug,
                        id = existingPayment?.Id
                    });
            }

            var hasAcceptedSubmission = await HasAcceptedSubmissionAsync(
                user.Id,
                registration.ConferenceId);

            if (!hasAcceptedSubmission)
            {
                TempData["InfoMessage"] = T(
                    "PaymentRequiresAcceptedSubmission",
                    "Ödeme ekranı, bildiriniz kabul edildikten sonra açılır.");

                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }

            var paymentModel = new Payment
            {
                ConferenceId = registration.ConferenceId,
                Conference = registration.Conference,
                RelatedSubmissionId = registration.Id,

                Amount = registration.RegistrationType?.Price ?? registration.Amount,
                Currency = registration.RegistrationType?.Currency ?? "TRY",

                BillingName = !string.IsNullOrWhiteSpace(registration.BillingName)
                    ? registration.BillingName
                    : $"{user.FirstName} {user.LastName}",

                BillingAddress = registration.BillingAddress,
                TaxNumber = registration.TaxNumber,
                TaxOffice = registration.TaxOffice
            };

            return View(paymentModel);
        }

        [HttpPost("Process")]
        [HttpPost("/{slug}/payment/process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Payment model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var registration = await _context.Registrations
                .Include(r => r.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r =>
                    r.Id == model.RelatedSubmissionId &&
                    r.AppUserId == user.Id);

            if (registration == null)
            {
                return NotFound(T("RegistrationForPaymentNotFound", "Ödeme yapılacak kongre kaydı bulunamadı."));
            }

            var slug = GetSlug();
            var canonicalSlug = GetCanonicalSlug(registration.Conference, slug);

            if (!SlugMatches(registration.Conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/payment/checkout/{registration.Id}"));
            }

            if (registration.IsPaid)
            {
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p =>
                        p.AppUserId == user.Id &&
                        p.ConferenceId == registration.ConferenceId &&
                        p.RelatedSubmissionId == registration.Id);

                return RedirectToAction(
                    nameof(Success),
                    new
                    {
                        slug = canonicalSlug,
                        id = existingPayment?.Id
                    });
            }

            var hasAcceptedSubmission = await HasAcceptedSubmissionAsync(
                user.Id,
                registration.ConferenceId);

            if (!hasAcceptedSubmission)
            {
                TempData["InfoMessage"] = T(
                    "PaymentRequiresAcceptedSubmission",
                    "Ödeme yapabilmek için önce bildirinizin kabul edilmesi gerekir.");

                return Redirect(BuildUrl(canonicalSlug, "/my-submissions"));
            }

            var amount = registration.RegistrationType?.Price ?? registration.Amount;
            var currency = registration.RegistrationType?.Currency ?? "TRY";

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = registration.ConferenceId,
                RelatedSubmissionId = registration.Id,

                PaymentDate = DateTime.UtcNow,
                Status = PaymentStatus.Completed,

                Amount = amount,
                Currency = currency,

                BillingName = model.BillingName,
                BillingAddress = model.BillingAddress,
                TaxNumber = model.TaxNumber,
                TaxOffice = model.TaxOffice,

                PaymentMethod = "CreditCard",
                TransactionId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
            };

            _context.Payments.Add(payment);

            registration.IsPaid = true;

            await _context.SaveChangesAsync();

            await _notificationService.CreateAsync(
                userId: user.Id,
                title: T("PaymentSuccessfulNotificationTitle", "Ödeme Başarılı"),
                message: string.Format(
                    T("PaymentSuccessfulNotificationMessage", "{0} {1} tutarındaki ödemeniz başarıyla tamamlandı."),
                    payment.Amount,
                    payment.Currency),
                icon: "fas fa-check-circle",
                color: "success",
                link: BuildUrl(canonicalSlug, "/payments")
            );

            return RedirectToAction(
                nameof(Success),
                new
                {
                    slug = canonicalSlug,
                    id = payment.Id
                });
        }

        [HttpGet("Success")]
        [HttpGet("/{slug}/payment/success")]
        public async Task<IActionResult> Success(Guid? id)
        {
            if (id == null)
            {
                return View();
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var payment = await _context.Payments
                .Include(p => p.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.AppUserId == user.Id);

            if (payment == null)
            {
                var slug = GetSlug();

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return Redirect(BuildUrl(slug, "/payments"));
                }

                return RedirectToAction(nameof(My));
            }

            var canonicalSlug = GetCanonicalSlug(payment.Conference, GetSlug());

            return View(payment);
        }

        [HttpGet("Cancel")]
        [HttpGet("/{slug}/payment/cancel")]
        public IActionResult Cancel()
        {
            return View();
        }
    }
}