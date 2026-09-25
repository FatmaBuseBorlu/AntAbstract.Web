#nullable disable

using AntAbstract.Domain.Entities;
using AntAbstract.Web.Infrastructure;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserStore<AppUser> _userStore;
        private readonly IUserEmailStore<AppUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly AppDbContext _context;

        private readonly EmailConfirmationSender _confirmationSender;
        private readonly ParticipantNotifier _participantNotifier;

        public ExternalLoginModel(
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<AppUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender,
            AppDbContext context,
            EmailConfirmationSender confirmationSender,
            ParticipantNotifier participantNotifier)
        {
            _participantNotifier = participantNotifier;
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _confirmationSender = confirmationSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ProviderDisplayName { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public IActionResult OnGet()
        {
            return RedirectToPage("./Login");
        }

        public async Task<IActionResult> OnPostAsync(string provider, string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
            if (string.IsNullOrWhiteSpace(provider) ||
                !schemes.Any(s => string.Equals(s.Name, provider, StringComparison.Ordinal)))
            {
                TempData["ErrorMessage"] =
                    "ORCID girişi şu anda yapılandırılmamış. Lütfen daha sonra tekrar deneyiniz.";

                return RedirectToPage(
                    "./Login",
                    new { ReturnUrl = returnUrl });
            }

            var redirectUrl = Url.Page(
                "./ExternalLogin",
                pageHandler: "Callback",
                values: new { returnUrl });

            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                provider,
                redirectUrl);

            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(
            string returnUrl = null,
            string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                TempData["ErrorMessage"] = $"Harici sağlayıcı hatası: {remoteError}";

                return RedirectToPage(
                    "./Login",
                    new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                TempData["ErrorMessage"] = "Harici sağlayıcıdan bilgi alınamadı.";

                return RedirectToPage(
                    "./Login",
                    new { ReturnUrl = returnUrl });
            }

            var externalUser = await _userManager.FindByLoginAsync(
                info.LoginProvider,
                info.ProviderKey);

            if (externalUser != null)
            {
                var blocked = await BlockedSignInAsync(externalUser, returnUrl);
                if (blocked != null)
                {
                    return blocked;
                }

                await UpdateOrcidProfileAsync(externalUser, info);

                await _signInManager.SignInAsync(
                    externalUser,
                    isPersistent: false,
                    authenticationMethod: info.LoginProvider);

                await EnsureAuthorRoleAsync(externalUser);

                var redirectAfterRegistration =
                    await TryCreateConferencePreRegistrationFromReturnUrlAsync(
                        externalUser,
                        returnUrl);

                if (!string.IsNullOrWhiteSpace(redirectAfterRegistration))
                {
                    return LocalRedirect(redirectAfterRegistration);
                }

                return LocalRedirect(GetSafeReturnUrl(returnUrl));
            }

            var email = GetExternalEmail(info);

            if (!string.IsNullOrWhiteSpace(email))
            {
                var existingUserByEmail = await _userManager.FindByEmailAsync(email);

                if (existingUserByEmail != null)
                {
                    var blocked = await BlockedSignInAsync(existingUserByEmail, returnUrl);
                    if (blocked != null)
                    {
                        return blocked;
                    }

                    var addLoginResult = await _userManager.AddLoginAsync(
                        existingUserByEmail,
                        info);

                    if (!addLoginResult.Succeeded)
                    {
                        foreach (var error in addLoginResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        TempData["ErrorMessage"] =
                            "ORCID hesabı bu kullanıcıya bağlanamadı. Lütfen tekrar deneyiniz.";

                        return RedirectToPage(
                            "./Login",
                            new { ReturnUrl = returnUrl });
                    }

                    await UpdateOrcidProfileAsync(existingUserByEmail, info);

                    await _signInManager.SignInAsync(
                        existingUserByEmail,
                        isPersistent: false,
                        authenticationMethod: info.LoginProvider);

                    await EnsureAuthorRoleAsync(existingUserByEmail);

                    var redirectAfterRegistration =
                        await TryCreateConferencePreRegistrationFromReturnUrlAsync(
                            existingUserByEmail,
                            returnUrl);

                    if (!string.IsNullOrWhiteSpace(redirectAfterRegistration))
                    {
                        return LocalRedirect(redirectAfterRegistration);
                    }

                    return LocalRedirect(GetSafeReturnUrl(returnUrl));
                }
            }

            /*
             * Kullanıcı sistemde hiç yoksa:
             * Normal Register ekranına gönderiyoruz.
             *
             * Önemli:
             * Bir sonraki adımda Register.cshtml.cs tarafına
             * bu TempData bilgilerini okuyup ORCID bağlantısını ekleyen kodu koyacağız.
             */
            TempData["ExternalLoginProvider"] = info.LoginProvider;
            TempData["ExternalProviderKey"] = info.ProviderKey;
            TempData["ExternalProviderDisplayName"] = info.ProviderDisplayName ?? info.LoginProvider;
            TempData["ExternalEmail"] = email ?? "";
            TempData["ExternalFirstName"] = GetExternalFirstName(info);
            TempData["ExternalLastName"] = GetExternalLastName(info);
            TempData["ExternalOrcidId"] = GetExternalOrcidId(info);

            var registerUrl = string.IsNullOrWhiteSpace(email)
                ? $"/register?returnUrl={Uri.EscapeDataString(returnUrl)}"
                : $"/register?returnUrl={Uri.EscapeDataString(returnUrl)}&email={Uri.EscapeDataString(email)}";

            return LocalRedirect(registerUrl);
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                ErrorMessage = "Harici giriş bilgisi alınamadı.";

                return RedirectToPage(
                    "./Login",
                    new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(
                    user,
                    Input.Email,
                    CancellationToken.None);

                await _emailStore.SetEmailAsync(
                    user,
                    Input.Email,
                    CancellationToken.None);

                user.EmailConfirmed = false;
                user.FirstName = GetExternalFirstName(info);
                user.LastName = GetExternalLastName(info);
                user.OrcidId = NormalizeOrcidId(GetExternalOrcidId(info));

                var result = await _userManager.CreateAsync(user);

                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation(
                            "Kullanıcı {Provider} sağlayıcısı ile hesap oluşturdu.",
                            info.LoginProvider);

                        await EnsureAuthorRoleAsync(user);

                        // Oturum açılmaz: kullanıcının yazdığı e-posta önce doğrulanmalı.
                        var redirectAfterRegistration =
                            await TryCreateConferencePreRegistrationFromReturnUrlAsync(
                                user,
                                returnUrl);

                        var afterConfirmUrl = !string.IsNullOrWhiteSpace(redirectAfterRegistration)
                            ? redirectAfterRegistration
                            : GetSafeReturnUrl(returnUrl);

                        var sent = await _confirmationSender.SendAsync(
                            user, Url, Request.Scheme, afterConfirmUrl);

                        TempData["RegisteredEmail"] = user.Email;
                        TempData["ConfirmationEmailFailed"] = !sent;

                        return RedirectToPage("./RegisterConfirmation");
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;

            return Page();
        }

        // SignInAsync doğrudan oturum açar; e-posta doğrulaması ve kilit
        // kontrolünü (PasswordSignInAsync'in yaptığı) burada biz yaparız.
        private async Task<IActionResult> BlockedSignInAsync(AppUser user, string returnUrl)
        {
            if (await _userManager.IsLockedOutAsync(user))
            {
                return RedirectToPage("./Lockout");
            }

            if (!await _signInManager.CanSignInAsync(user))
            {
                TempData["ErrorMessage"] =
                    System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                        ? "Your email address is not confirmed yet. Please click the link in the email we sent you."
                        : "E-posta adresiniz henüz doğrulanmadı. Size gönderdiğimiz e-postadaki bağlantıya tıklayın.";

                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            return null;
        }

        private async Task EnsureAuthorRoleAsync(AppUser user)
        {
            const string authorRoleName = "Author";

            var roleExists = await _roleManager.RoleExistsAsync(authorRoleName);

            if (!roleExists)
            {
                await _roleManager.CreateAsync(new IdentityRole(authorRoleName));
            }

            var userIsAuthor = await _userManager.IsInRoleAsync(
                user,
                authorRoleName);

            if (!userIsAuthor)
            {
                await _userManager.AddToRoleAsync(
                    user,
                    authorRoleName);
            }
        }

        private async Task<string> TryCreateConferencePreRegistrationFromReturnUrlAsync(
            AppUser user,
            string returnUrl)
        {
            var returnPath = NormalizeReturnUrlPath(returnUrl);

            if (string.IsNullOrWhiteSpace(returnPath))
            {
                return null;
            }

            var parts = returnPath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                return null;
            }

            var slug = parts[0];
            var registrationSegment = parts[1];
            var checkoutSegment = parts[2];
            var typeIdSegment = parts[3];

            var isRegistrationRoute =
                registrationSegment.Equals("registration", StringComparison.OrdinalIgnoreCase) ||
                registrationSegment.Equals("register", StringComparison.OrdinalIgnoreCase);

            var isCheckoutRoute =
                checkoutSegment.Equals("checkout", StringComparison.OrdinalIgnoreCase);

            if (!isRegistrationRoute || !isCheckoutRoute)
            {
                return null;
            }

            if (!Guid.TryParse(typeIdSegment, out var registrationTypeId))
            {
                return null;
            }

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(rt =>
                    rt.Id == registrationTypeId &&
                    rt.IsActive &&
                    (!rt.Deadline.HasValue || rt.Deadline.Value >= DateTime.UtcNow));

            if (ticketType == null || ticketType.Conference == null)
            {
                return null;
            }

            var conference = ticketType.Conference;

            var slugMatchesConference =
                string.Equals(conference.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(conference.Tenant?.Slug, slug, StringComparison.OrdinalIgnoreCase);

            if (!slugMatchesConference)
            {
                return null;
            }

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.AppUserId == user.Id &&
                    r.ConferenceId == conference.Id);

            if (existingRegistration == null)
            {
                var newRegistration = new Registration
                {
                    Id = Guid.NewGuid(),
                    AppUserId = user.Id,
                    ConferenceId = conference.Id,
                    RegistrationTypeId = ticketType.Id,
                    RegistrationDate = DateTime.UtcNow,
                    IsPaid = false,
                    Amount = ticketType.Price
                };

                _context.Registrations.Add(newRegistration);
                await _context.SaveChangesAsync();

                // Doğrulanmamış adrese e-posta atılmaz (o anda doğrulama
                // e-postası gidiyor); sistem içi bildirim her durumda düşer.
                await _participantNotifier.RegistrationReceivedAsync(newRegistration.Id, sendEmail: user.EmailConfirmed);
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            TempData["SuccessMessage"] =
                "ORCID ile giriş başarılı. Kongre ön kaydınız oluşturuldu. Şimdi bildirinizin özetini gönderebilirsiniz.";

            return $"/{canonicalSlug}/submit-abstract";
        }

        private static string NormalizeReturnUrlPath(string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return string.Empty;
            }

            var decodedReturnUrl = Uri.UnescapeDataString(returnUrl);

            if (Uri.TryCreate(decodedReturnUrl, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri.AbsolutePath;
            }

            if (decodedReturnUrl.StartsWith("~/", StringComparison.Ordinal))
            {
                decodedReturnUrl = decodedReturnUrl[1..];
            }

            var queryIndex = decodedReturnUrl.IndexOf('?');

            if (queryIndex >= 0)
            {
                decodedReturnUrl = decodedReturnUrl[..queryIndex];
            }

            return decodedReturnUrl;
        }

        private void SetSelectedConferenceSession(
            Conference conference,
            string slug)
        {
            HttpContext.Session.SetString(
                "SelectedConferenceId",
                conference.Id.ToString());

            HttpContext.Session.SetString(
                "SelectedConferenceSlug",
                slug);

            HttpContext.Session.SetString(
                "SelectedConferenceTitle",
                conference.Title ?? "");

            HttpContext.Session.SetString(
                $"SelectedConferenceId:{conference.TenantId}",
                conference.Id.ToString());

            HttpContext.Session.SetString(
                $"SelectedConferenceSlug:{conference.TenantId}",
                slug);

            HttpContext.Session.SetString(
                $"SelectedConferenceTitle:{conference.TenantId}",
                conference.Title ?? "");
        }

        private string GetSafeReturnUrl(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return Url.Content("~/");
        }

        private async Task UpdateOrcidProfileAsync(AppUser user, ExternalLoginInfo info)
        {
            var changed = false;
            var orcidId = NormalizeOrcidId(GetExternalOrcidId(info));

            if (!string.IsNullOrWhiteSpace(orcidId) &&
                !string.Equals(user.OrcidId, orcidId, StringComparison.OrdinalIgnoreCase))
            {
                user.OrcidId = orcidId;
                changed = true;
            }

            var firstName = GetExternalFirstName(info);
            if (string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(firstName))
            {
                user.FirstName = firstName;
                changed = true;
            }

            var lastName = GetExternalLastName(info);
            if (string.IsNullOrWhiteSpace(user.LastName) && !string.IsNullOrWhiteSpace(lastName))
            {
                user.LastName = lastName;
                changed = true;
            }

            if (changed)
            {
                await _userManager.UpdateAsync(user);
            }
        }

        private static string GetExternalEmail(ExternalLoginInfo info)
        {
            return info.Principal.FindFirstValue(ClaimTypes.Email)
                   ?? info.Principal.FindFirstValue("email")
                   ?? info.Principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        }

        private static string GetExternalFirstName(ExternalLoginInfo info)
        {
            return info.Principal.FindFirstValue(ClaimTypes.GivenName)
                   ?? info.Principal.FindFirstValue("given_name")
                   ?? "";
        }

        private static string GetExternalLastName(ExternalLoginInfo info)
        {
            return info.Principal.FindFirstValue(ClaimTypes.Surname)
                   ?? info.Principal.FindFirstValue("family_name")
                   ?? "";
        }

        private static string GetExternalOrcidId(ExternalLoginInfo info)
        {
            return info.Principal.FindFirstValue("orcid")
                   ?? info.Principal.FindFirstValue("sub")
                   ?? info.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? info.ProviderKey
                   ?? "";
        }

        private static string NormalizeOrcidId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            var marker = "orcid.org/";
            var markerIndex = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (markerIndex >= 0)
            {
                trimmed = trimmed[(markerIndex + marker.Length)..];
            }

            return trimmed.Length > 50 ? trimmed[..50] : trimmed;
        }

        private AppUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AppUser>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Can't create an instance of '{nameof(AppUser)}'. " +
                    $"Ensure that '{nameof(AppUser)}' is not an abstract class and has a parameterless constructor, " +
                    $"or alternatively override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        private IUserEmailStore<AppUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(
                    "The default UI requires a user store with email support.");
            }

            return (IUserEmailStore<AppUser>)_userStore;
        }
    }
}
