using System;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace AntAbstract.Web.Controllers
{
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly TenantContext _tenantContext;
        private readonly IStringLocalizer<RegistrationController> _localizer;

        public RegistrationController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<AppUser> signInManager,
            TenantContext tenantContext,
            IStringLocalizer<RegistrationController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _tenantContext = tenantContext;
            _localizer = localizer;
        }

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

        private static string BuildUrl(string slug, string path)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return path;
            }

            return $"/{slug}{path}";
        }

        private async Task EnsureAuthorRoleAsync(AppUser user)
        {
            const string authorRoleName = "Author";

            var roleExists = await _roleManager.RoleExistsAsync(authorRoleName);

            if (!roleExists)
            {
                await _roleManager.CreateAsync(new IdentityRole(authorRoleName));
            }

            var userIsAuthor = await _userManager.IsInRoleAsync(user, authorRoleName);

            if (!userIsAuthor)
            {
                await _userManager.AddToRoleAsync(user, authorRoleName);

                /*
                 * Kullanıcıya rol verdikten sonra giriş bilgisini yeniliyoruz.
                 * Böylece kullanıcı hemen /submit-abstract sayfasına erişebilir.
                 */
                await _signInManager.RefreshSignInAsync(user);
            }
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

        [AllowAnonymous]
        [HttpGet("/{slug}/register")]
        [HttpGet("/{slug}/registration")]
        public async Task<IActionResult> Index()
        {
            var slug = GetSlug();

            var conference = await GetConferenceBySlugAsync(slug);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "ConferenceNotFound",
                    "Kongre bulunamadı.");

                return RedirectToAction("Index", "Home");
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);

                if (user != null)
                {
                    var existingRegistration = await _context.Registrations
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r =>
                            r.ConferenceId == conference.Id &&
                            r.AppUserId == user.Id);

                    if (existingRegistration != null)
                    {
                        await EnsureAuthorRoleAsync(user);

                        if (existingRegistration.IsPaid)
                        {
                            TempData["SuccessMessage"] = T(
                                "AlreadyRegisteredAndPaid",
                                "Bu kongreye kaydınız ve ödemeniz zaten tamamlanmış.");
                        }
                        else
                        {
                            TempData["InfoMessage"] = T(
                                "AlreadyRegisteredCanSubmitAbstract",
                                "Bu kongreye kaydınız zaten var. Şimdi bildirinizin özetini gönderebilirsiniz.");
                        }

                        return Redirect(BuildUrl(canonicalSlug, "/submit-abstract"));
                    }
                }
            }

            var registrationTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(rt =>
                    rt.ConferenceId == conference.Id &&
                    rt.IsActive &&
                    (!rt.Deadline.HasValue || rt.Deadline.Value >= DateTime.UtcNow))
                .OrderBy(rt => rt.Price)
                .ThenBy(rt => rt.Name)
                .ToListAsync();

            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.ConferenceStartDate = conference.StartDate;
            ViewBag.Slug = canonicalSlug;

            return View(registrationTypes);
        }

        [AllowAnonymous]
        [HttpGet("/{slug}/register/join")]
        [HttpGet("/{slug}/registration/join")]
        public async Task<IActionResult> Join()
        {
            return await Index();
        }

        /*
         * ÖNEMLİ:
         * Bu GET action artık AllowAnonymous.
         *
         * Sebep:
         * Kullanıcı giriş yapmadan "Ön Kayda Devam Et" dediğinde
         * doğrudan login ekranına değil, register ekranına returnUrl ile gitsin.
         *
         * Böylece kullanıcı sisteme kayıt olurken aynı anda kongre ön kaydı da oluşturulabilir.
         */
        [AllowAnonymous]
        [HttpGet("/{slug}/register/checkout/{typeId:guid}")]
        [HttpGet("/{slug}/registration/checkout/{typeId:guid}")]
        public async Task<IActionResult> Checkout(Guid typeId)
        {
            var slug = GetSlug();

            var returnUrl = BuildUrl(slug, $"/registration/checkout/{typeId}");

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Redirect($"/register?returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(rt =>
                    rt.Id == typeId &&
                    rt.IsActive &&
                    (!rt.Deadline.HasValue || rt.Deadline.Value >= DateTime.UtcNow));

            if (ticketType == null)
            {
                TempData["ErrorMessage"] = T(
                    "InvalidOrExpiredTicket",
                    "Geçersiz veya süresi dolmuş kayıt türü.");

                return Redirect(BuildUrl(slug, "/registration"));
            }

            var conference = ticketType.Conference;

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "ConferenceNotFound",
                    "Kongre bulunamadı.");

                return Redirect(BuildUrl(slug, "/registration"));
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            var existingRegistration = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == ticketType.ConferenceId &&
                    r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                await EnsureAuthorRoleAsync(user);

                TempData["InfoMessage"] = T(
                    "AlreadyRegisteredCanSubmitAbstract",
                    "Bu kongreye kaydınız zaten var. Şimdi bildirinizin özetini gönderebilirsiniz.");

                return Redirect(BuildUrl(canonicalSlug, "/submit-abstract"));
            }

            ViewBag.Ticket = ticketType;
            ViewBag.User = user;
            ViewBag.Slug = canonicalSlug;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.ConferenceStartDate = conference.StartDate;

            return View(new Registration
            {
                RegistrationTypeId = typeId,
                ConferenceId = ticketType.ConferenceId,
                AppUserId = user.Id,
                Amount = ticketType.Price,
                IsPaid = false,
                RegistrationDate = DateTime.UtcNow
            });
        }

        /*
         * POST tarafı Authorize kalmalı.
         *
         * Çünkü gerçek ön kayıt veritabanına yazılırken
         * kullanıcının sisteme giriş yapmış olması gerekir.
         */
        [Authorize]
        [HttpPost("/{slug}/register/checkout/{typeId:guid}")]
        [HttpPost("/{slug}/registration/checkout/{typeId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutPost(
            Guid typeId,
            string? BillingName,
            string? TaxOffice,
            string? TaxNumber,
            string? BillingAddress)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var slug = GetSlug();

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(rt =>
                    rt.Id == typeId &&
                    rt.IsActive &&
                    (!rt.Deadline.HasValue || rt.Deadline.Value >= DateTime.UtcNow));

            if (ticketType == null)
            {
                TempData["ErrorMessage"] = T(
                    "InvalidOrExpiredTicket",
                    "Geçersiz veya süresi dolmuş kayıt türü.");

                return Redirect(BuildUrl(slug, "/registration"));
            }

            var conference = ticketType.Conference;

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "ConferenceNotFound",
                    "Kongre bulunamadı.");

                return Redirect(BuildUrl(slug, "/registration"));
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == ticketType.ConferenceId &&
                    r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                await EnsureAuthorRoleAsync(user);

                TempData["InfoMessage"] = T(
                    "AlreadyRegisteredCanSubmitAbstract",
                    "Bu kongreye kaydınız zaten var. Şimdi bildirinizin özetini gönderebilirsiniz.");

                return Redirect(BuildUrl(canonicalSlug, "/submit-abstract"));
            }

            var newRegistration = new Registration
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = ticketType.ConferenceId,
                RegistrationTypeId = ticketType.Id,
                RegistrationDate = DateTime.UtcNow,
                IsPaid = false,
                Amount = ticketType.Price,

                BillingName = string.IsNullOrWhiteSpace(BillingName) ? null : BillingName.Trim(),
                TaxOffice = string.IsNullOrWhiteSpace(TaxOffice) ? null : TaxOffice.Trim(),
                TaxNumber = string.IsNullOrWhiteSpace(TaxNumber) ? null : TaxNumber.Trim(),
                BillingAddress = string.IsNullOrWhiteSpace(BillingAddress) ? null : BillingAddress.Trim()
            };

            _context.Registrations.Add(newRegistration);
            await _context.SaveChangesAsync();

            await EnsureAuthorRoleAsync(user);

            TempData["SuccessMessage"] = T(
                "RegistrationSuccessSubmitAbstract",
                "Kongre kaydınız başarıyla oluşturuldu. Şimdi bildirinizin özetini gönderebilirsiniz.");

            return Redirect(BuildUrl(canonicalSlug, "/submit-abstract"));
        }
    }
}