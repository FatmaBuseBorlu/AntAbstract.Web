using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
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
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;
        private readonly IUploadFileValidator _uploadFileValidator;

        public PaymentController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            INotificationService notificationService,
            IStringLocalizer<PaymentController> localizer,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            IUploadFileValidator uploadFileValidator)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _notificationService = notificationService;
            _localizer = localizer;
            _env = env;
            _configuration = configuration;
            _logger = logger;
            _uploadFileValidator = uploadFileValidator;
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
                .Where(p =>
                    p.AppUserId == userId &&
                    p.ConferenceId == registration.ConferenceId &&
                    p.RelatedSubmissionId == registration.Id)
                .OrderByDescending(p => p.Status == PaymentStatus.Completed)
                .ThenByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();
        }

        private static string BuildPaymentSuccessUrl(string canonicalSlug, Guid? paymentId = null)
        {
            if (paymentId.HasValue && paymentId.Value != Guid.Empty)
            {
                return $"/{canonicalSlug}/payment/success?id={paymentId.Value}";
            }

            return $"/{canonicalSlug}/payment/success";
        }

        private static bool IsConfiguredSecret(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !value.StartsWith("#{", StringComparison.Ordinal);
        }

        private bool IsStripeConfigured()
        {
            return IsConfiguredSecret(_configuration["Stripe:SecretKey"]);
        }

        private string GetPublicBaseUrl()
        {
            var configuredBaseUrl = _configuration["Stripe:BaseUrl"]
                                    ?? _configuration["Email:BaseUrl"];

            if (!string.IsNullOrWhiteSpace(configuredBaseUrl) &&
                Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredUri))
            {
                return configuredUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            }

            return $"{Request.Scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');
        }

        private static long ToMinorUnit(decimal amount)
        {
            if (amount <= 0)
            {
                throw new InvalidOperationException("Ödeme tutarı sıfırdan büyük olmalıdır.");
            }

            return checked((long)Math.Round(
                amount * 100m,
                0,
                MidpointRounding.AwayFromZero));
        }

        private async Task<bool> CompleteStripePaymentAsync(
            Guid paymentId,
            string checkoutSessionId,
            string? paymentIntentId,
            long? amountTotal,
            string? currency)
        {
            var payment = await _context.Payments
                .Include(p => p.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null || payment.Status == PaymentStatus.Refunded)
            {
                return false;
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(payment.TransactionId) &&
                !string.Equals(
                    payment.TransactionId,
                    checkoutSessionId,
                    StringComparison.Ordinal))
            {
                _logger.LogError(
                    "Stripe session mismatch for payment {PaymentId}. Expected {ExpectedSessionId}, received {SessionId}.",
                    payment.Id,
                    payment.TransactionId,
                    checkoutSessionId);

                return false;
            }

            if (amountTotal.HasValue &&
                amountTotal.Value != ToMinorUnit(payment.Amount))
            {
                _logger.LogError(
                    "Stripe amount mismatch for payment {PaymentId}. Expected {ExpectedAmount}, received {Amount}.",
                    payment.Id,
                    ToMinorUnit(payment.Amount),
                    amountTotal.Value);

                return false;
            }

            if (!string.IsNullOrWhiteSpace(currency) &&
                !string.Equals(
                    payment.Currency,
                    currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "Stripe currency mismatch for payment {PaymentId}. Expected {ExpectedCurrency}, received {Currency}.",
                    payment.Id,
                    payment.Currency,
                    currency);

                return false;
            }

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.Id == payment.RelatedSubmissionId &&
                    r.AppUserId == payment.AppUserId &&
                    r.ConferenceId == payment.ConferenceId);

            if (registration == null)
            {
                _logger.LogError(
                    "Stripe payment {PaymentId} has no matching registration.",
                    payment.Id);

                return false;
            }

            var now = DateTime.UtcNow;

            payment.Status = PaymentStatus.Completed;
            payment.PaymentDate = now;
            payment.TransactionId = checkoutSessionId;

            registration.IsPaid = true;
            registration.Status = AntAbstract.Domain.Entities.RegistrationStatus.Confirmed;
            registration.PaymentDate = now;
            registration.PaymentTransactionId = !string.IsNullOrWhiteSpace(paymentIntentId)
                ? paymentIntentId
                : checkoutSessionId;

            await _context.SaveChangesAsync();

            var canonicalSlug = GetCanonicalSlug(payment.Conference);

            try
            {
                await _notificationService.CreateAsync(
                    userId: payment.AppUserId,
                    title: T("PaymentSuccessfulNotificationTitle", "Ödeme Başarılı"),
                    message: string.Format(
                        T("PaymentSuccessfulNotificationMessage", "{0} {1} tutarındaki ödemeniz başarıyla tamamlandı."),
                        payment.Amount,
                        payment.Currency),
                    icon: "fas fa-check-circle",
                    color: "success",
                    link: BuildUrl(canonicalSlug, "/payments"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Payment {PaymentId} completed but notification could not be created.",
                    payment.Id);
            }

            return true;
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

            if (!IsStripeConfigured())
            {
                _logger.LogError("Stripe secret key is not configured.");

                TempData["ErrorMessage"] = T(
                    "StripeNotConfigured",
                    "Online ödeme sistemi şu anda yapılandırılmamış. Lütfen kongre yönetimiyle iletişime geçin.");

                return Redirect(BuildUrl(canonicalSlug, $"/payment/checkout/{registration.Id}"));
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
                Status = PaymentStatus.Pending,

                Amount = amount,
                Currency = currency,

                BillingName = !string.IsNullOrWhiteSpace(model.BillingName)
                    ? model.BillingName
                    : $"{user.FirstName} {user.LastName}".Trim(),

                BillingAddress = model.BillingAddress,
                TaxNumber = model.TaxNumber,
                TaxOffice = model.TaxOffice,

                PaymentMethod = "StripeCheckout"
            };

            _context.Payments.Add(payment);

            registration.BillingName = payment.BillingName;
            registration.BillingAddress = payment.BillingAddress;
            registration.TaxNumber = payment.TaxNumber;
            registration.TaxOffice = payment.TaxOffice;

            await _context.SaveChangesAsync();

            var baseUrl = GetPublicBaseUrl();
            var successUrl =
                $"{baseUrl}/{canonicalSlug}/payment/success?session_id={{CHECKOUT_SESSION_ID}}";
            var cancelUrl =
                $"{baseUrl}/{canonicalSlug}/payment/cancel?paymentId={payment.Id}";

            var metadata = new Dictionary<string, string>
            {
                ["payment_id"] = payment.Id.ToString(),
                ["registration_id"] = registration.Id.ToString(),
                ["conference_id"] = registration.ConferenceId.ToString(),
                ["user_id"] = user.Id
            };

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerEmail = user.Email,
                ClientReferenceId = payment.Id.ToString(),
                Metadata = metadata,
                PaymentMethodTypes = new List<string> { "card" },
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = metadata
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = currency.ToLowerInvariant(),
                            UnitAmount = ToMinorUnit(amount),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = registration.Conference?.Title
                                       ?? T("ConferenceRegistration", "Kongre katılım kaydı"),
                                Description = registration.RegistrationType?.Name
                            }
                        }
                    }
                }
            };

            try
            {
                var sessionService = new SessionService();
                var checkoutSession = await sessionService.CreateAsync(
                    options,
                    new RequestOptions
                    {
                        IdempotencyKey = payment.Id.ToString("N")
                    });

                if (string.IsNullOrWhiteSpace(checkoutSession.Url))
                {
                    throw new StripeException("Stripe Checkout URL oluşturmadı.");
                }

                payment.TransactionId = checkoutSession.Id;
                await _context.SaveChangesAsync();

                return Redirect(checkoutSession.Url);
            }
            catch (Exception ex)
            {
                payment.Status = PaymentStatus.Failed;
                await _context.SaveChangesAsync();

                _logger.LogError(
                    ex,
                    "Stripe Checkout session could not be created for payment {PaymentId}.",
                    payment.Id);

                TempData["ErrorMessage"] = T(
                    "StripeSessionCreateFailed",
                    "Güvenli ödeme sayfası açılamadı. Lütfen tekrar deneyin.");

                return Redirect(BuildUrl(canonicalSlug, $"/payment/checkout/{registration.Id}"));
            }
        }

        [HttpGet("Success")]
        [HttpGet("/{slug}/payment/success")]
        public async Task<IActionResult> Success(string? session_id, Guid? id)
        {
            var slug = GetSlug();

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            Payment? payment = null;

            if (!string.IsNullOrWhiteSpace(session_id))
            {
                payment = await _context.Payments
                    .Include(p => p.Conference)
                        .ThenInclude(c => c.Tenant)
                    .FirstOrDefaultAsync(p =>
                        p.TransactionId == session_id &&
                        p.AppUserId == user.Id);

                if (payment != null &&
                    payment.Status != PaymentStatus.Completed &&
                    IsStripeConfigured())
                {
                    try
                    {
                        var sessionService = new SessionService();
                        var checkoutSession = await sessionService.GetAsync(session_id);

                        if (string.Equals(
                                checkoutSession.PaymentStatus,
                                "paid",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            await CompleteStripePaymentAsync(
                                payment.Id,
                                checkoutSession.Id,
                                checkoutSession.PaymentIntentId,
                                checkoutSession.AmountTotal,
                                checkoutSession.Currency);

                            payment = await _context.Payments
                                .Include(p => p.Conference)
                                    .ThenInclude(c => c.Tenant)
                                .FirstOrDefaultAsync(p => p.Id == payment.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Stripe session {SessionId} could not be verified on the success page.",
                            session_id);
                    }
                }
            }
            else if (id.HasValue)
            {
                payment = await _context.Payments
                    .Include(p => p.Conference)
                        .ThenInclude(c => c.Tenant)
                    .FirstOrDefaultAsync(p =>
                        p.Id == id.Value &&
                        p.AppUserId == user.Id);
            }

            if (payment == null)
            {
                return Redirect(BuildUrl(slug, "/payments"));
            }

            var canonicalSlug = GetCanonicalSlug(payment.Conference, slug);

            if (payment.Conference != null)
            {
                SetSelectedConferenceSession(payment.Conference, canonicalSlug);
            }

            return View(payment);
        }

        [AllowAnonymous]
        [HttpPost("/payment/stripe-webhook")]
        [EnableRateLimiting("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            if (!IsConfiguredSecret(webhookSecret))
            {
                _logger.LogError("Stripe webhook secret is not configured.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();

            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    webhookSecret);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid Stripe webhook signature.");
                return BadRequest();
            }

            if (stripeEvent.Data.Object is not Stripe.Checkout.Session checkoutSession)
            {
                return Ok();
            }

            if (!checkoutSession.Metadata.TryGetValue("payment_id", out var paymentIdText) ||
                !Guid.TryParse(paymentIdText, out var paymentId))
            {
                _logger.LogWarning(
                    "Stripe event {EventId} has no valid payment_id metadata.",
                    stripeEvent.Id);

                return Ok();
            }

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                case "checkout.session.async_payment_succeeded":
                    if (string.Equals(
                            checkoutSession.PaymentStatus,
                            "paid",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await CompleteStripePaymentAsync(
                            paymentId,
                            checkoutSession.Id,
                            checkoutSession.PaymentIntentId,
                            checkoutSession.AmountTotal,
                            checkoutSession.Currency);
                    }
                    break;

                case "checkout.session.expired":
                case "checkout.session.async_payment_failed":
                    var payment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.Id == paymentId);

                    if (payment != null && payment.Status == PaymentStatus.Pending)
                    {
                        payment.Status = PaymentStatus.Failed;
                        await _context.SaveChangesAsync();
                    }
                    break;
            }

            return Ok();
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
        [RequestSizeLimit(20 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
        [EnableRateLimiting("upload")]
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

            var validation = await _uploadFileValidator.ValidateAsync(
                receiptFile,
                UploadFileProfile.PaymentReceipt);

            if (!validation.IsValid)
            {
                var errorMessage = validation.Error switch
                {
                    UploadValidationError.TooLarge =>
                        "Makbuz dosyası en fazla 5 MB olabilir.",
                    UploadValidationError.InvalidExtension =>
                        "Yalnızca PDF, PNG veya JPG dosyası yükleyebilirsiniz.",
                    _ =>
                        "Makbuz dosyasının içeriği seçilen formatla eşleşmiyor."
                };

                ModelState.AddModelError("", errorMessage);
                ViewBag.Registration = registration;
                ViewBag.Slug = slug ?? registration.Conference?.Tenant?.Slug ?? "";
                return View();
            }

            var folder = PrivateStorage.EnsureFolder(_env, PrivateStorage.ReceiptsFolder);

            // Önceki makbuz varsa diskten sil
            if (!string.IsNullOrWhiteSpace(registration.ReceiptFilePath))
            {
                var oldPhysical = PrivateStorage.Resolve(_env, registration.ReceiptFilePath);
                if (System.IO.File.Exists(oldPhysical))
                    System.IO.File.Delete(oldPhysical);
            }

            var fileName = _uploadFileValidator.CreateStoredFileName(
                validation.Extension,
                $"receipt-{registrationId:N}");
            var filePath = Path.Combine(folder, fileName);
            using (var fs = new FileStream(filePath, FileMode.Create))
                await receiptFile.CopyToAsync(fs);

            registration.ReceiptFilePath = PrivateStorage.ToRelativePath(PrivateStorage.ReceiptsFolder, fileName);
            registration.ReceiptUploadedAt = DateTime.UtcNow;
            registration.Status = AntAbstract.Domain.Entities.RegistrationStatus.AwaitingApproval;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Makbuzunuz başarıyla yüklendi. Yönetici onayından sonra kaydınız aktif olacaktır.";
            return RedirectToAction(nameof(My));
        }

        [HttpGet("Cancel")]
        [HttpGet("/{slug}/payment/cancel")]
        public async Task<IActionResult> Cancel(Guid? paymentId)
        {
            if (paymentId.HasValue)
            {
                var user = await _userManager.GetUserAsync(User);

                if (user != null)
                {
                    var payment = await _context.Payments
                        .FirstOrDefaultAsync(p =>
                            p.Id == paymentId.Value &&
                            p.AppUserId == user.Id);

                    if (payment != null && payment.Status == PaymentStatus.Pending)
                    {
                        payment.Status = PaymentStatus.Failed;
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return View();
        }
    }
}
