#nullable disable

using AntAbstract.Domain.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using AntAbstract.Infrastructure.Context;
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

        public ExternalLoginModel(
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserStore<AppUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender,
            AppDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
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

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

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

                user.EmailConfirmed = true;

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

                        var userId = await _userManager.GetUserIdAsync(user);

                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                        code = WebEncoders.Base64UrlEncode(
                            Encoding.UTF8.GetBytes(code));

                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new
                            {
                                area = "Identity",
                                userId,
                                code
                            },
                            protocol: Request.Scheme);

                        await _emailSender.SendEmailAsync(
                            Input.Email,
                            "Confirm your email",
                            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                        if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        {
                            return RedirectToPage(
                                "./RegisterConfirmation",
                                new { Email = Input.Email });
                        }

                        await _signInManager.SignInAsync(
                            user,
                            isPersistent: false,
                            authenticationMethod: info.LoginProvider);

                        var redirectAfterRegistration =
                            await TryCreateConferencePreRegistrationFromReturnUrlAsync(
                                user,
                                returnUrl);

                        if (!string.IsNullOrWhiteSpace(redirectAfterRegistration))
                        {
                            return LocalRedirect(redirectAfterRegistration);
                        }

                        return LocalRedirect(GetSafeReturnUrl(returnUrl));
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
