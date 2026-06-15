using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.IO;
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
        private readonly IWebHostEnvironment _env;

        public PaymentController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            INotificationService notificationService,
            IStringLocalizer<PaymentController> localizer,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _notificationService = notificationService;
            _localizer = localizer;
            _env = env;
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
                var tenantSpecificKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                conferenceIdText = HttpContext.Session.GetString(tenantSpecificKey);
            }

            conferenceIdText ??= HttpContext.Session.GetString("SelectedConferenceId");

            return Guid.TryParse(conferenceIdText, out var parsedId) && parsedId != Guid.Empty
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

            var encodedReturnUrl = Uri.EscapeDataString(returnUrl);

            var url = string.IsNullOrWhiteSpace(slug)
                ? $"/Dashboard/MyConferences?returnUrl={encodedReturnUrl}"
                : $"/{slug}/Dashboard/MyConferences?returnUrl={encodedReturnUrl}";

            return Redirect(url);
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
                .Where(c =>
                    c.Slug == slug ||
                    (c.Tenant != null && c.Tenant.Slug == slug))
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();
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

                if (selectedConference != null && SlugMatches(selectedConference, slug))
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

        private async Task<Payment?> GetExistingPaymentAsync(string userId, Registration registration)
        {
            return await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.AppUserId == userId &&
                    p.ConferenceId == registration.ConferenceId &&
                    p.RelatedSubmissionId == registration.Id);
        }

        private static string BuildPaymentSuccessUrl(string canonicalSlug, Guid? paymentId = null)
        {
            if (paymentId.HasValue && paymentId.Value != Guid.Empty)
            {
                return $"/{canonicalSlug}/payment/success?id={paymentId.Value}";
            }

            return $"/{canonicalSlug}/payment/success";
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

                return Redirect(BuildUrl(canonicalSlug, "/registration"));
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
                var existingPayment = await GetExistingPaymentAsync(user.Id, registration);

                return Redirect(BuildPaymentSuccessUrl(canonicalSlug, existingPayment?.Id));
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
                    : $"{user.FirstName} {user.LastName}".Trim(),

                BillingAddress = registration.BillingAddress,
                TaxNumber = registration.TaxNumber,
                TaxOffice = registration.TaxOffice,

                PaymentMethod = "CreditCard"
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

            if (registration.Conference != null)
            {
                SetSelectedConferenceSession(registration.Conference, canonicalSlug);
            }

            if (registration.IsPaid)
            {
                var existingPayment = await GetExistingPaymentAsync(user.Id, registration);

                return Redirect(BuildPaymentSuccessUrl(canonicalSlug, existingPayment?.Id));
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

            var transactionId = Guid.NewGuid()
                .ToString("N")
                .Substring(0, 12)
                .ToUpper();

            var paymentDate = DateTime.UtcNow;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = registration.ConferenceId,
                RelatedSubmissionId = registration.Id,

                PaymentDate = paymentDate,
                Status = PaymentStatus.Completed,

                Amount = amount,
                Currency = currency,

                BillingName = !string.IsNullOrWhiteSpace(model.BillingName)
                    ? model.BillingName
                    : $"{user.FirstName} {user.LastName}".Trim(),

                BillingAddress = model.BillingAddress,
                TaxNumber = model.TaxNumber,
                TaxOffice = model.TaxOffice,

                PaymentMethod = "CreditCard",
                TransactionId = transactionId
            };

            _context.Payments.Add(payment);

            registration.IsPaid = true;
            registration.PaymentDate = paymentDate;
            registration.PaymentTransactionId = transactionId;

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

            return Redirect(BuildPaymentSuccessUrl(canonicalSlug, payment.Id));
        }

        [HttpGet("Success")]
        [HttpGet("/{slug}/payment/success")]
        public async Task<IActionResult> Success(Guid? id)
        {
            var slug = GetSlug();

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
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    return Redirect(BuildUrl(slug, "/payments"));
                }

                return RedirectToAction(nameof(My));
            }

            var canonicalSlug = GetCanonicalSlug(payment.Conference, slug);

            if (payment.Conference != null)
            {
                SetSelectedConferenceSession(payment.Conference, canonicalSlug);
            }

            return View(payment);
        }

        // ─── Makbuz Yükleme ────────────────────────────────────────────────────
        [HttpGet("/payment/upload-receipt/{registrationId:guid}")]
        [HttpGet("/{slug}/payment/upload-receipt/{registrationId:guid}")]
        public async Task<IActionResult> UploadReceipt(Guid registrationId, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.RegistrationType)
                .Include(r => r.Conference).ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.AppUserId == user.Id);

            if (registration == null) return NotFound();
            if (registration.IsPaid)
            {
                TempData["InfoMessage"] = "Bu kayıt zaten ödenmiş olarak işaretlenmiş.";
                return RedirectToAction(nameof(My));
            }

            ViewBag.Registration = registration;
            ViewBag.Slug = slug ?? registration.Conference?.Tenant?.Slug ?? registration.Conference?.Slug ?? "";
            return View();
        }

        [HttpPost("/payment/upload-receipt/{registrationId:guid}")]
        [HttpPost("/{slug}/payment/upload-receipt/{registrationId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadReceipt(Guid registrationId, IFormFile? receiptFile, string? slug = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var registration = await _context.Registrations
                .Include(r => r.Conference).ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.AppUserId == user.Id);

            if (registration == null) return NotFound();

            if (receiptFile == null || receiptFile.Length == 0)
            {
                ModelState.AddModelError("", "Lütfen makbuz dosyası seçin.");
                ViewBag.Registration = registration;
                ViewBag.Slug = slug ?? registration.Conference?.Tenant?.Slug ?? "";
                return View();
            }

            // Dosya boyutu: maks 5 MB
            const long MaxReceiptSize = 5 * 1024 * 1024;
            if (receiptFile.Length > MaxReceiptSize)
            {
                ModelState.AddModelError("", "Makbuz dosyası en fazla 5 MB olabilir.");
                ViewBag.Registration = registration;
                ViewBag.Slug = slug ?? registration.Conference?.Tenant?.Slug ?? "";
                return View();
            }

            // Yalnızca PDF, PNG, JPG
            var ext = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
            if (!new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(ext))
            {
                ModelState.AddModelError("", "Yalnızca PDF, PNG veya JPG dosyası yükleyebilirsiniz.");
                ViewBag.Registration = registration;
                ViewBag.Slug = slug ?? registration.Conference?.Tenant?.Slug ?? "";
                return View();
            }

            // Kaydet
            var folder = Path.Combine(_env.WebRootPath, "uploads", "receipts");
            Directory.CreateDirectory(folder);
            var fileName = $"{registrationId:N}{ext}";
            var filePath = Path.Combine(folder, fileName);
            using (var fs = new FileStream(filePath, FileMode.Create))
                await receiptFile.CopyToAsync(fs);

            registration.ReceiptFilePath = $"/uploads/receipts/{fileName}";
            registration.ReceiptUploadedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Makbuzunuz başarıyla yüklendi. Yönetici onayından sonra kaydınız aktif olacaktır.";
            return RedirectToAction(nameof(My));
        }

        [HttpGet("Cancel")]
        [HttpGet("/{slug}/payment/cancel")]
        public IActionResult Cancel()
        {
            return View();
        }
    }
}