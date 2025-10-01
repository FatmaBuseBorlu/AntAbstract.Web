using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly AppDbContext _context;

        public PaymentController(AppDbContext context)
        {
            _context = context;
        }

        // ÖDEME BAŞARILI OLDUĞUNDA STRIPE'IN YÖNLENDİRECEĞİ SAYFA
        public async Task<IActionResult> Success(string sessionId)
        {
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(sessionId);

            // Ödeme oturumundan bizim kayıt ID'mizi al
            var registrationId = Guid.Parse(session.ClientReferenceId);

            var registration = await _context.Registrations.FindAsync(registrationId);
            if (registration != null)
            {
                // Kaydın durumunu "Ödendi" olarak güncelle
                registration.IsPaid = true;
                registration.PaymentDate = DateTime.UtcNow;
                registration.PaymentTransactionId = session.PaymentIntentId;
                await _context.SaveChangesAsync();
            }

            return View(); // Başarı sayfasını göster
        }

        // ÖDEME İPTAL EDİLDİĞİNDE STRIPE'IN YÖNLENDİRECEĞİ SAYFA
        public IActionResult Cancel()
        {
            return View(); // İptal sayfasını göster
        }

        // KULLANICININ "ŞİMDİ ÖDE" BUTONUNA BASTIĞINDA ÇALIŞACAK OLAN METOT
        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(Guid registrationId)
        {
            var registration = await _context.Registrations
                .Include(r => r.RegistrationType)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (registration == null) return NotFound();

            var domain = $"{Request.Scheme}://{Request.Host}";

            var options = new SessionCreateOptions
            {
                // Müşteri bilgileri (isteğe bağlı)
                CustomerEmail = User.Identity.Name,
                // Bizim sistemimizdeki kayıt ID'sini Stripe'a gizlice gönderiyoruz ki,
                // ödeme başarılı olduğunda hangi kaydı güncelleyeceğimizi bilelim.
                ClientReferenceId = registration.Id.ToString(),
                // Ödeme tamamlandığında veya iptal edildiğinde kullanıcıyı yönlendireceğimiz URL'ler
                SuccessUrl = $"{domain}/Payment/Success?sessionId={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/Payment/Cancel",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            // Ürün fiyatı (Stripe kuruş/cent cinsinden çalışır, bu yüzden 100 ile çarpıyoruz)
                            UnitAmount = (long)(registration.RegistrationType.Price * 100),
                            Currency = registration.RegistrationType.Currency.ToLower(),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = registration.RegistrationType.Name,
                                Description = registration.RegistrationType.Description,
                            },

                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
            };

            var service = new SessionService();
            Stripe.Checkout.Session session = await service.CreateAsync(options); 

            // Kullanıcıyı Stripe'ın ödeme sayfasına yönlendir
            return Redirect(session.Url);
        }
    }
}