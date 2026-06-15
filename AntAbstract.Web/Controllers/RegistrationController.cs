using System;
using System.Linq;
using System.Threading.Tasks;
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
        private const int MaxBillingNameLength = 200;
        private const int MaxTaxOfficeLength = 100;
        private const int MaxTaxNumberLength = 50;
        private const int MaxBillingAddressLength = 500;

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

        private static string BuildUrl(string? slug, string path)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return path;
            }

            return $"/{slug}{path}";
        }

        private static string GetCanonicalSlug(Conference conference, string? fallbackSlug = null)
        {
            return conference.Tenant?.Slug
                   ?? conference.Slug
                   ?? fallbackSlug
                   ?? "";
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

        private static string? NormalizeNullable(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();

            if (value.Length > maxLength)
            {
                value = value.Substring(0, maxLength);
            }

            return value;
        }

        private static bool IsDeadlineOpen(DateTime? deadline)
        {
            if (!deadline.HasValue)
            {
                return true;
            }

            return deadline.Value.Date >= DateTime.UtcNow.Date;
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

        private async Task EnsureAuthorRoleAsync(AppUser user)
        {
            await EnsureRoleAsync(user, "Author");
        }

        /// <summary>
        /// Kullanıcıya belirtilen rolü atar. Rol yoksa önce oluşturur.
        /// Geçerli roller: Author, Listener. Bilinmeyen roller için Author varsayılır.
        /// </summary>
        private async Task EnsureRoleAsync(AppUser user, string roleName)
        {
            // Güvenlik: yalnızca izin verilen katılımcı rollerine atama yapılır
            var allowed = new[] { "Author", "Listener", "Dinleyici", "Yazar" };

            if (!allowed.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            {
                roleName = "Author";
            }

            var roleExists = await _roleManager.RoleExistsAsync(roleName);

            if (!roleExists)
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }

            var alreadyInRole = await _userManager.IsInRoleAsync(user, roleName);

            if (!alreadyInRole)
            {
                await _userManager.AddToRoleAsync(user, roleName);

                await _signInManager.RefreshSignInAsync(user);
            }
        }

        /// <summary>
        /// Kayıt türündeki RoleName bilgisine göre kullanıcıya doğru rolü atar.
        /// </summary>
        private async Task EnsureRoleFromRegistrationTypeAsync(AppUser user, Guid registrationTypeId)
        {
            var regType = await _context.RegistrationTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == registrationTypeId);

            var roleName = !string.IsNullOrWhiteSpace(regType?.RoleName)
                ? regType.RoleName
                : "Author";

            await EnsureRoleAsync(user, roleName);
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

        private async Task<Conference?> FindConferenceByIdAsync(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return null;
            }

            return await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);
        }

        private async Task<Conference?> GetConferenceBySlugAsync(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c =>
                    c.Slug == slug ||
                    (
                        c.Tenant != null &&
                        c.Tenant.Slug == slug
                    ))
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();
        }

        private async Task<Conference?> ResolveConferenceAsync(string? slug)
        {
            if (!string.IsNullOrWhiteSpace(slug))
            {
                var tenantId = _tenantContext.Current?.Id;
                var selectedConferenceId = GetSelectedConferenceIdFromSession(tenantId);

                if (selectedConferenceId.HasValue)
                {
                    var selectedConference = await FindConferenceByIdAsync(selectedConferenceId.Value);

                    if (selectedConference != null && SlugMatches(selectedConference, slug))
                    {
                        return selectedConference;
                    }
                }
            }

            return await GetConferenceBySlugAsync(slug);
        }

        private async Task<RegistrationType?> GetValidRegistrationTypeAsync(Guid typeId)
        {
            return await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(rt =>
                    rt.Id == typeId &&
                    rt.IsActive);
        }

        private async Task<Registration?> GetExistingRegistrationAsync(string userId, Guid conferenceId)
        {
            return await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == conferenceId &&
                    r.AppUserId == userId);
        }

        [AllowAnonymous]
        [HttpGet("/{slug}/register")]
        [HttpGet("/{slug}/registration")]
        public async Task<IActionResult> Index()
        {
            var slug = GetSlug();

            var conference = await ResolveConferenceAsync(slug);

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "ConferenceNotFound",
                    "Kongre bulunamadı.");

                return RedirectToAction("Index", "Home");
            }

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            SetSelectedConferenceSession(conference, canonicalSlug);

            if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(BuildUrl(canonicalSlug, "/registration"));
            }

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
                        if (existingRegistration.RegistrationTypeId != Guid.Empty)
                        {
                            await EnsureRoleFromRegistrationTypeAsync(user, existingRegistration.RegistrationTypeId);
                        }
                        else
                        {
                            await EnsureAuthorRoleAsync(user);
                        }

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

            var today = DateTime.UtcNow.Date;

            var registrationTypes = await _context.RegistrationTypes
                .AsNoTracking()
                .Where(rt =>
                    rt.ConferenceId == conference.Id &&
                    rt.IsActive &&
                    (
                        !rt.Deadline.HasValue ||
                        rt.Deadline.Value.Date >= today
                    ))
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

            var ticketType = await GetValidRegistrationTypeAsync(typeId);

            if (ticketType == null || !IsDeadlineOpen(ticketType.Deadline))
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

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!SlugMatches(conference, slug))
            {
                return Redirect(BuildUrl(canonicalSlug, $"/registration/checkout/{typeId}"));
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

            var existingRegistration = await _context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.ConferenceId == ticketType.ConferenceId &&
                    r.AppUserId == user.Id);

            if (existingRegistration != null)
            {
                await EnsureRoleFromRegistrationTypeAsync(user, ticketType.Id);

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

            var ticketType = await GetValidRegistrationTypeAsync(typeId);

            if (ticketType == null || !IsDeadlineOpen(ticketType.Deadline))
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

            var canonicalSlug = GetCanonicalSlug(conference, slug);

            if (!SlugMatches(conference, slug))
            {
                TempData["ErrorMessage"] = T(
                    "InvalidConferenceSelection",
                    "Seçilen kayıt türü bu kongreye ait değil.");

                return Redirect(BuildUrl(canonicalSlug, $"/registration/checkout/{typeId}"));
            }

            SetSelectedConferenceSession(conference, canonicalSlug);

            var existingRegistration = await GetExistingRegistrationAsync(
                user.Id,
                ticketType.ConferenceId);

            if (existingRegistration != null)
            {
                await EnsureRoleFromRegistrationTypeAsync(user, ticketType.Id);

                TempData["InfoMessage"] = T(
                    "AlreadyRegisteredCanSubmitAbstract",
                    "Bu kongreye kaydınız zaten var. Şimdi bildirinizin özetini gönderebilirsiniz.");

                return Redirect(BuildUrl(canonicalSlug, "/submit-abstract"));
            }

            // Kota kontrolü
            if (!conference.IsRegistrationOpen)
            {
                TempData["ErrorMessage"] = T(
                    "RegistrationClosed",
                    "Bu kongreye kayıt şu anda kapalıdır.");
                return Redirect(BuildUrl(canonicalSlug, "/registration"));
            }

            if (conference.MaxRegistrations.HasValue)
            {
                var currentCount = await _context.Registrations
                    .AsNoTracking()
                    .CountAsync(r => r.ConferenceId == conference.Id);

                if (currentCount >= conference.MaxRegistrations.Value)
                {
                    TempData["ErrorMessage"] = T(
                        "RegistrationQuotaFull",
                        "Bu kongre için kayıt kontenjanı dolmuştur.");
                    return Redirect(BuildUrl(canonicalSlug, "/registration"));
                }
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

                BillingName = NormalizeNullable(BillingName, MaxBillingNameLength),
                TaxOffice = NormalizeNullable(TaxOffice, MaxTaxOfficeLength),
                TaxNumber = NormalizeNullable(TaxNumber, MaxTaxNumberLength),
                BillingAddress = NormalizeNullable(BillingAddress, MaxBillingAddressLength)
            };

            _context.Registrations.Add(newRegistration);

            await _context.SaveChangesAsync();

            // Kayıt türüne göre doğru rolü ata (Yazar veya Dinleyici)
            await EnsureRoleFromRegistrationTypeAsync(user, ticketType.Id);

            TempData["SuccessMessage"] = T(
                "RegistrationSuccessSubmitAbstract",
                "Kongre kaydınız başarıyla oluşturuldu. Şimdi bildirinizin özetini gönderebilirsiniz.");

            return Redirect(BuildUrl(canonicalSlug, $"/registration/success?id={newRegistration.Id}"));
        }

        [Authorize]
        [HttpGet("/{slug}/registration/success")]
        [HttpGet("/{slug}/register/success")]
        public async Task<IActionResult> Success(Guid? id = null)
        {
            var slug = GetSlug();
            var canonicalSlug = slug;

            if (id.HasValue && id.Value != Guid.Empty)
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return Challenge();
                }

                var registration = await _context.Registrations
                    .AsNoTracking()
                    .Include(r => r.Conference)
                        .ThenInclude(c => c.Tenant)
                    .FirstOrDefaultAsync(r =>
                        r.Id == id.Value &&
                        r.AppUserId == user.Id);

                if (registration == null)
                {
                    TempData["ErrorMessage"] = T(
                        "RegistrationNotFound",
                        "Kayıt bulunamadı.");

                    return Redirect(BuildUrl(slug, "/registration"));
                }

                if (registration.Conference != null)
                {
                    canonicalSlug = GetCanonicalSlug(registration.Conference, slug);

                    if (!SlugMatches(registration.Conference, slug))
                    {
                        return Redirect(BuildUrl(canonicalSlug, $"/registration/success?id={id.Value}"));
                    }

                    SetSelectedConferenceSession(registration.Conference, canonicalSlug);
                }

                ViewBag.RegistrationId = registration.Id;
            }
            else
            {
                var conference = await ResolveConferenceAsync(slug);

                if (conference != null)
                {
                    canonicalSlug = GetCanonicalSlug(conference, slug);

                    SetSelectedConferenceSession(conference, canonicalSlug);

                    if (!string.Equals(canonicalSlug, slug, StringComparison.OrdinalIgnoreCase))
                    {
                        return Redirect(BuildUrl(canonicalSlug, "/registration/success"));
                    }
                }
            }

            ViewBag.Slug = canonicalSlug;

            return View();
        }
    }
}