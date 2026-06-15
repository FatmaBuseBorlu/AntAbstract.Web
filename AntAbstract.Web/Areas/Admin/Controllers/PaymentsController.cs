using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Email;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
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

        public PaymentsController(
            AppDbContext context,
            IEmailService emailService,
            IAdminTenantAccessService tenantAccess)
        {
            _context = context;
            _emailService = emailService;
            _tenantAccess = tenantAccess;
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
            ViewBag.TotalCount     = allForConf.Count;
            ViewBag.PaidCount      = allForConf.Count(r => r.IsPaid);
            ViewBag.ReceiptCount   = allForConf.Count(r => !r.IsPaid && r.ReceiptFilePath != null);
            ViewBag.PendingCount   = allForConf.Count(r => !r.IsPaid && r.ReceiptFilePath == null);
            ViewBag.TotalRevenue   = allForConf.Where(r => r.IsPaid).Sum(r => r.Amount);

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

            if (!alreadyExists)
            {
                _context.Payments.Add(new Payment
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
                });
            }

            await _context.SaveChangesAsync();

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
            catch { /* email hatası işlemi durdurmaz */ }

            TempData["SuccessMessage"] = "Ödeme başarıyla onaylandı ve kullanıcıya e-posta gönderildi.";
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

            // Makbuzu temizle, notu sakla
            registration.ReceiptFilePath = null;
            registration.ReceiptUploadedAt = null;
            registration.AdminPaymentNote = note;
            await _context.SaveChangesAsync();

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
            catch { }

            TempData["SuccessMessage"] = "Makbuz reddedildi ve kullanıcıya bildirim gönderildi.";
            return RedirectBack(returnUrl);
        }

        private IActionResult RedirectBack(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Index));
        }
    }
}
