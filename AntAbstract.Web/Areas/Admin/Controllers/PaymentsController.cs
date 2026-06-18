using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Infrastructure.Services.Invoice;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class PaymentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IAdminTenantAccessService _tenantAccess;
        private readonly IAuditService _audit;
        private readonly UserManager<AppUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IInvoicePdfService _invoicePdfService;

        public PaymentsController(
            AppDbContext context,
            IEmailService emailService,
            IAdminTenantAccessService tenantAccess,
            IAuditService audit,
            UserManager<AppUser> userManager,
            INotificationService notificationService,
            ILogger<PaymentsController> logger,
            IInvoicePdfService invoicePdfService)
        {
            _context = context;
            _emailService = emailService;
            _tenantAccess = tenantAccess;
            _audit = audit;
            _userManager = userManager;
            _notificationService = notificationService;
            _logger = logger;
            _invoicePdfService = invoicePdfService;
        }

        // GET /{slug}/Admin/Payments  —  tüm ödemeler + makbuz bekleyenler
        public async Task<IActionResult> Index(
            string? slug,
            Guid? conferenceId,
            string? status = "all",
            string? search = null)
        {
            var accessibleConferences = await _tenantAccess
                .GetAccessibleConferenceQueryAsync(User);

            // Kongreyi belirle
            Conference? conference = null;
            if (conferenceId.HasValue && conferenceId != Guid.Empty)
            {
                conference = await accessibleConferences
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Id == conferenceId);
            }
            else if (!string.IsNullOrWhiteSpace(slug))
            {
                conference = await accessibleConferences
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Slug == slug || (c.Tenant != null && c.Tenant.Slug == slug));
            }

            if (conference == null)
            {
                // Konferans listesi sun
                var conferences = await accessibleConferences
                    .Include(c => c.Tenant)
                    .OrderByDescending(c => c.StartDate)
                    .Take(20)
                    .ToListAsync();
                ViewBag.Conferences = conferences;
                return View("SelectConference");
            }

            ViewBag.Conference = conference;
            ViewBag.Slug = conference.Tenant?.Slug ?? conference.Slug ?? slug ?? "";
            ViewBag.Status = status;
            ViewBag.Search = search;

            var query = _context.Registrations
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Where(r => r.ConferenceId == conference.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(r =>
                    (r.AppUser != null && (
                        (r.AppUser.FirstName != null && r.AppUser.FirstName.Contains(s)) ||
                        (r.AppUser.LastName != null && r.AppUser.LastName.Contains(s)) ||
                        (r.AppUser.Email != null && r.AppUser.Email.Contains(s)))) ||
                    (r.BillingName != null && r.BillingName.Contains(s)));
            }

            if (status == "pending")
                query = query.Where(r => !r.IsPaid);
            else if (status == "paid")
                query = query.Where(r => r.IsPaid);
            else if (status == "receipt")
                query = query.Where(r => !r.IsPaid && r.ReceiptFilePath != null);

            var registrations = await query
                .OrderByDescending(r => r.ReceiptUploadedAt)
                .ThenByDescending(r => r.RegistrationDate)
                .ToListAsync();

            // Özet istatistik
            var allForConf = await _context.Registrations.AsNoTracking()
                .Where(r => r.ConferenceId == conference.Id).ToListAsync();
            ViewBag.TotalCount = allForConf.Count;
            ViewBag.PaidCount = allForConf.Count(r => r.IsPaid);
            ViewBag.ReceiptCount = allForConf.Count(r => !r.IsPaid && r.ReceiptFilePath != null);
            ViewBag.PendingCount = allForConf.Count(r => !r.IsPaid && r.ReceiptFilePath == null);
            ViewBag.TotalRevenue = allForConf.Where(r => r.IsPaid).Sum(r => r.Amount);

            // userId → PaymentId haritası (History butonları için)
            var userIds = registrations.Where(r => r.IsPaid && r.AppUserId != null)
                .Select(r => r.AppUserId!).Distinct().ToList();
            var paymentMap = await _context.Payments
                .Where(p => p.ConferenceId == conference.Id && userIds.Contains(p.AppUserId))
                .GroupBy(p => p.AppUserId)
                .Select(g => new { UserId = g.Key, PaymentId = g.OrderByDescending(p => p.PaymentDate).First().Id })
                .ToDictionaryAsync(x => x.UserId, x => (Guid?)x.PaymentId);
            ViewBag.PaymentMap = paymentMap;

            return View(registrations);
        }

        // POST — Ödemeyi Onayla
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid registrationId, string? note, string? returnUrl)
        {
            var accessibleRegistrations = await _tenantAccess
                .GetAccessibleRegistrationQueryAsync(User);
            var registration = await accessibleRegistrations
                .Include(r => r.AppUser)
                .Include(r => r.RegistrationType)
                .Include(r => r.Conference).ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration == null)
            {
                TempData["ErrorMessage"] = "Kayıt bulunamadı.";
                return RedirectBack(returnUrl);
            }

            if (registration.IsPaid)
            {
                TempData["InfoMessage"] = "Bu kayıt zaten ödenmiş.";
                return RedirectBack(returnUrl);
            }

            var now = DateTime.UtcNow;
            var shortId = registration.Id.ToString("N")[..8];
            var txId = $"MANUAL-{now:yyyyMMddHHmmss}-{shortId}";

            registration.IsPaid = true;
            registration.Status = RegistrationStatus.Confirmed;
            registration.PaymentDate = now;
            registration.PaymentTransactionId = txId;
            registration.AdminPaymentNote = note;

            if (registration.Amount <= 0 && registration.RegistrationType != null)
                registration.Amount = registration.RegistrationType.Price;

            // Payment kaydı
            var alreadyExists = await _context.Payments.AnyAsync(p =>
                p.ConferenceId == registration.ConferenceId &&
                p.AppUserId == registration.AppUserId &&
                p.Status == PaymentStatus.Completed);

            Payment? approvedPayment = null;
            if (!alreadyExists)
            {
                approvedPayment = new Payment
                {
                    Amount = registration.Amount,
                    Currency = registration.RegistrationType?.Currency ?? "TRY",
                    PaymentMethod = "BankTransfer",
                    TransactionId = txId,
                    PaymentDate = now,
                    Status = PaymentStatus.Completed,
                    BillingName = registration.BillingName,
                    BillingAddress = registration.BillingAddress,
                    TaxOffice = registration.TaxOffice,
                    TaxNumber = registration.TaxNumber,
                    AppUserId = registration.AppUserId,
                    ConferenceId = registration.ConferenceId
                };
                _context.Payments.Add(approvedPayment);
            }
            else
            {
                approvedPayment = await _context.Payments.FirstOrDefaultAsync(p =>
                    p.ConferenceId == registration.ConferenceId &&
                    p.AppUserId == registration.AppUserId &&
                    p.Status == PaymentStatus.Completed);
            }

            await _context.SaveChangesAsync();

            // Durum geçmişi kaydet
            if (approvedPayment != null)
            {
                var adminForHistory = await _userManager.GetUserAsync(User);
                _context.PaymentStatusHistories.Add(new PaymentStatusHistory
                {
                    PaymentId = approvedPayment.Id,
                    OldStatus = PaymentStatus.Pending,
                    NewStatus = PaymentStatus.Completed,
                    ChangedByUserId = adminForHistory?.Id,
                    ChangedByUserName = adminForHistory != null
                        ? $"{adminForHistory.FirstName} {adminForHistory.LastName}".Trim() : null,
                    Note = note,
                    Source = "Admin"
                });
                await _context.SaveChangesAsync();
            }

            // E-posta bildir
            try
            {
                var user = registration.AppUser;
                if (user?.Email != null)
                {
                    var fullName = $"{user.FirstName} {user.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = user.Email;

                    await _emailService.SendAsync(user.Email,
                        $"Ödemeniz Onaylandı — {registration.Conference?.Title}",
                        $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
                          <div style='background:#16a34a;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
                            <h2 style='margin:0'>✅ Ödemeniz Onaylandı</h2>
                          </div>
                          <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
                            <p>Sayın <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p>
                            <p><strong>{System.Net.WebUtility.HtmlEncode(registration.Conference?.Title ?? "")}</strong>
                               kongresine ait ödemeniz yönetici tarafından onaylanmıştır.</p>
                            <p><strong>Tutar:</strong> {registration.Amount:N2} {registration.RegistrationType?.Currency ?? "TRY"}</p>
                            {(string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Not:</strong> {System.Net.WebUtility.HtmlEncode(note)}</p>")}
                            <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                          </div>
                        </div>");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ödeme onay e-postası gönderilemedi. RegistrationId={Id}", registrationId);
            }

            TempData["SuccessMessage"] = "Ödeme başarıyla onaylandı ve kullanıcıya e-posta gönderildi.";

            // In-app bildirim
            if (registration.AppUserId != null)
            {
                try
                {
                    await _notificationService.CreateAsync(
                        userId: registration.AppUserId,
                        title: "Ödemeniz Onaylandı ✅",
                        message: $"{registration.Conference?.Title} kongresine ait ödemeniz onaylandı.",
                        icon: "✅",
                        color: "success",
                        link: "/Registration/Details/" + registration.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ödeme onay bildirimi gönderilemedi. RegistrationId={Id}", registrationId);
                }
            }

            var adminUser = await _userManager.GetUserAsync(User);
            await _audit.LogAsync(
                category: "Payment",
                action: "PaymentApproved",
                userId: adminUser?.Id,
                userName: adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                entityType: "Registration",
                entityId: registrationId.ToString(),
                description: $"Manuel ödeme onayı: {registration.Amount:N2} {registration.RegistrationType?.Currency ?? "TRY"} — {registration.AppUser?.Email}",
                conferenceId: registration.ConferenceId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return RedirectBack(returnUrl);
        }

        // POST — Ödemeyi Reddet / İptal Et
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid registrationId, string? note, string? returnUrl)
        {
            var accessibleRegistrations = await _tenantAccess
                .GetAccessibleRegistrationQueryAsync(User);
            var registration = await accessibleRegistrations
                .Include(r => r.AppUser)
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration == null)
            {
                TempData["ErrorMessage"] = "Kayıt bulunamadı.";
                return RedirectBack(returnUrl);
            }

            // Makbuzu temizle, notu sakla, durumu güncelle
            registration.ReceiptFilePath = null;
            registration.ReceiptUploadedAt = null;
            registration.AdminPaymentNote = note;
            registration.Status = RegistrationStatus.PaymentRejected;
            await _context.SaveChangesAsync();

            // Durum geçmişi — ilgili Payment kaydı varsa ekle
            var rejectedPayment = await _context.Payments
                .Where(p => p.ConferenceId == registration.ConferenceId &&
                            p.AppUserId == registration.AppUserId)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();
            if (rejectedPayment != null)
            {
                var adminForHistory = await _userManager.GetUserAsync(User);
                _context.PaymentStatusHistories.Add(new PaymentStatusHistory
                {
                    PaymentId = rejectedPayment.Id,
                    OldStatus = rejectedPayment.Status,
                    NewStatus = PaymentStatus.Failed,
                    ChangedByUserId = adminForHistory?.Id,
                    ChangedByUserName = adminForHistory != null
                        ? $"{adminForHistory.FirstName} {adminForHistory.LastName}".Trim() : null,
                    Note = note,
                    Source = "Admin"
                });
                await _context.SaveChangesAsync();
            }

            // Kullanıcıya bildir
            try
            {
                var user = registration.AppUser;
                if (user?.Email != null)
                {
                    var fullName = $"{user.FirstName} {user.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = user.Email;

                    await _emailService.SendAsync(user.Email,
                        $"Ödeme Makbuzu Hakkında — {registration.Conference?.Title}",
                        $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
                          <div style='background:#dc2626;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
                            <h2 style='margin:0'>❌ Makbuz Onaylanamadı</h2>
                          </div>
                          <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
                            <p>Sayın <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p>
                            <p>Yüklediğiniz ödeme makbuzu onaylanamamıştır. Lütfen tekrar yükleyiniz.</p>
                            {(string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Neden:</strong> {System.Net.WebUtility.HtmlEncode(note)}</p>")}
                            <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                          </div>
                        </div>");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Makbuz ret e-postası gönderilemedi. RegistrationId={Id}", registrationId);
            }

            TempData["SuccessMessage"] = "Makbuz reddedildi ve kullanıcıya bildirim gönderildi.";

            // In-app bildirim
            if (registration.AppUserId != null)
            {
                try
                {
                    await _notificationService.CreateAsync(
                        userId: registration.AppUserId,
                        title: "Makbuzunuz Onaylanamadı ❌",
                        message: "Yüklediğiniz ödeme makbuzu onaylanamamıştır. Lütfen tekrar yükleyiniz.",
                        icon: "❌",
                        color: "danger",
                        link: "/Registration/Details/" + registration.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Makbuz ret bildirimi gönderilemedi. RegistrationId={Id}", registrationId);
                }
            }

            var adminUser2 = await _userManager.GetUserAsync(User);
            await _audit.LogAsync(
                category: "Payment",
                action: "PaymentRejected",
                userId: adminUser2?.Id,
                userName: adminUser2 != null ? $"{adminUser2.FirstName} {adminUser2.LastName}".Trim() : null,
                entityType: "Registration",
                entityId: registrationId.ToString(),
                description: $"Makbuz reddedildi: {registration.AppUser?.Email} — Not: {note}",
                conferenceId: registration.ConferenceId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return RedirectBack(returnUrl);
        }

        // POST — Kaydı İptal Et
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid registrationId, string? note, string? returnUrl)
        {
            var accessibleRegistrations = await _tenantAccess
                .GetAccessibleRegistrationQueryAsync(User);
            var registration = await accessibleRegistrations
                .Include(r => r.AppUser)
                .Include(r => r.Conference)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration == null)
            {
                TempData["ErrorMessage"] = "Kayıt bulunamadı.";
                return RedirectBack(returnUrl);
            }

            if (registration.Status == RegistrationStatus.Cancelled)
            {
                TempData["InfoMessage"] = "Bu kayıt zaten iptal edilmiş.";
                return RedirectBack(returnUrl);
            }

            registration.Status = RegistrationStatus.Cancelled;
            registration.AdminPaymentNote = string.IsNullOrWhiteSpace(note)
                ? registration.AdminPaymentNote
                : note;
            await _context.SaveChangesAsync();

            // Durum geçmişi
            var cancelledPayment = await _context.Payments
                .Where(p => p.ConferenceId == registration.ConferenceId &&
                            p.AppUserId == registration.AppUserId)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();
            if (cancelledPayment != null)
            {
                var adminForHistory = await _userManager.GetUserAsync(User);
                _context.PaymentStatusHistories.Add(new PaymentStatusHistory
                {
                    PaymentId = cancelledPayment.Id,
                    OldStatus = cancelledPayment.Status,
                    NewStatus = PaymentStatus.Refunded,
                    ChangedByUserId = adminForHistory?.Id,
                    ChangedByUserName = adminForHistory != null
                        ? $"{adminForHistory.FirstName} {adminForHistory.LastName}".Trim() : null,
                    Note = note,
                    Source = "Admin"
                });
                await _context.SaveChangesAsync();
            }

            // Kullanıcıya bildir
            try
            {
                var user = registration.AppUser;
                if (user?.Email != null)
                {
                    var fullName = $"{user.FirstName} {user.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = user.Email;

                    await _emailService.SendAsync(user.Email,
                        $"Kongre Kaydınız İptal Edildi — {registration.Conference?.Title}",
                        $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
                          <div style='background:#111827;color:#fff;padding:24px 32px;border-radius:8px 8px 0 0'>
                            <h2 style='margin:0'>🚫 Kaydınız İptal Edildi</h2>
                          </div>
                          <div style='background:#f9fafb;padding:24px 32px;border-radius:0 0 8px 8px'>
                            <p>Sayın <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p>
                            <p><strong>{System.Net.WebUtility.HtmlEncode(registration.Conference?.Title ?? "")}</strong>
                               kongresine ait kaydınız yönetici tarafından iptal edilmiştir.</p>
                            {(string.IsNullOrWhiteSpace(note) ? "" : $"<p><strong>Açıklama:</strong> {System.Net.WebUtility.HtmlEncode(note)}</p>")}
                            <p>Daha fazla bilgi için lütfen kongre organizasyonu ile iletişime geçiniz.</p>
                            <p style='margin-top:24px;color:#6b7280;font-size:13px'>Bu e-posta otomatik olarak gönderilmiştir.</p>
                          </div>
                        </div>");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kayıt iptal e-postası gönderilemedi. RegistrationId={Id}", registrationId);
            }

            TempData["SuccessMessage"] = "Kayıt iptal edildi ve kullanıcıya bildirim gönderildi.";

            // In-app bildirim
            if (registration.AppUserId != null)
            {
                try
                {
                    await _notificationService.CreateAsync(
                        userId: registration.AppUserId,
                        title: "Kongre Kaydınız İptal Edildi 🚫",
                        message: $"{registration.Conference?.Title} kongresine ait kaydınız iptal edildi.",
                        icon: "🚫",
                        color: "dark",
                        link: "/Registration/Details/" + registration.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Kayıt iptal bildirimi gönderilemedi. RegistrationId={Id}", registrationId);
                }
            }

            var adminUser = await _userManager.GetUserAsync(User);
            await _audit.LogAsync(
                category: "Registration",
                action: "RegistrationCancelled",
                userId: adminUser?.Id,
                userName: adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                entityType: "Registration",
                entityId: registrationId.ToString(),
                description: $"Kayıt iptal edildi: {registration.AppUser?.Email} — Not: {note}",
                conferenceId: registration.ConferenceId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return RedirectBack(returnUrl);
        }

        // GET /{slug}/Admin/Payments/History?paymentId=...
        public async Task<IActionResult> History(Guid paymentId)
        {
            var accessibleConfs = await _tenantAccess.GetAccessibleConferenceQueryAsync(User);

            var payment = await _context.Payments
                .Include(p => p.Conference)
                .Include(p => p.AppUser)
                .Where(p => accessibleConfs.Select(c => c.Id).Contains(p.ConferenceId))
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
                return NotFound();

            var history = await _context.PaymentStatusHistories
                .Where(h => h.PaymentId == paymentId)
                .OrderBy(h => h.ChangedAt)
                .ToListAsync();

            ViewBag.Payment = payment;
            return View(history);
        }

        // GET /{slug}/Admin/Payments/WebhookLog
        public async Task<IActionResult> WebhookLog(
            string? eventType = null,
            string? status = null,
            int page = 1)
        {
            // Yalnızca SuperAdmin veya TenantAdmin erişebilir
            // (TenantAdmin politikası zaten controller seviyesinde)
            const int pageSize = 50;

            var query = _context.StripeWebhookEvents
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(e => e.EventType.Contains(eventType));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(e => e.Status == status);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.ReceivedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.EventType = eventType;
            ViewBag.Status = status;

            return View(items);
        }

        private IActionResult RedirectBack(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Index));
        }

        // Admin: Fatura PDF indirme
        [HttpGet("/{slug}/Admin/Payments/DownloadInvoice/{registrationId:guid}")]
        [HttpGet("/Admin/Payments/DownloadInvoice/{registrationId:guid}")]
        public async Task<IActionResult> DownloadInvoice(string? slug, Guid registrationId)
        {
            var registration = await _context.Registrations
                .Include(r => r.Conference)
                    .ThenInclude(c => c.Tenant)
                .Include(r => r.RegistrationType)
                .Include(r => r.AppUser)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration == null)
                return NotFound();

            if (!registration.IsPaid)
                return BadRequest("Ödeme tamamlanmamış kayıtlar için fatura oluşturulamaz.");

            var pdfBytes = _invoicePdfService.GenerateRegistrationInvoice(registration);
            var fileName = $"Fatura-{registration.Id.ToString("N").Substring(0, 8).ToUpper()}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
