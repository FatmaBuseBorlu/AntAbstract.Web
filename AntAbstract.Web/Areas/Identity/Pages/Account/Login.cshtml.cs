using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IAuditService _audit;

        public LoginModel(
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            ILogger<LoginModel> logger,
            IAuditService audit)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _audit = audit;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "E-Posta adresi zorunludur.")]
            [EmailAddress]
            public string Email { get; set; }

            [Required(ErrorMessage = "Şifre zorunludur.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Beni Hatırla?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            // Varsayılan olarak Dashboard'a yönlendir
            returnUrl ??= Url.Content("~/Dashboard");

            // Çerezleri temizle
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            // Giriş başarılı olursa Dashboard'a git
            returnUrl ??= Url.Content("~/Dashboard");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // PasswordSignInAsync ile giriş denemesi
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Kullanıcı giriş yaptı.");
                    var loggedUser = await _userManager.FindByEmailAsync(Input.Email);
                    _ = _audit.LogAsync(
                        category: "Auth",
                        action: "Login",
                        userId: loggedUser?.Id,
                        userName: loggedUser != null ? $"{loggedUser.FirstName} {loggedUser.LastName}".Trim() : Input.Email,
                        description: "Başarılı giriş",
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Kullanıcı hesabı kilitlendi.");
                    _ = _audit.LogAsync(
                        category: "Auth",
                        action: "LoginLockedOut",
                        description: $"Kilitli hesap giriş denemesi: {Input.Email}",
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    _ = _audit.LogAsync(
                        category: "Auth",
                        action: "LoginFailed",
                        description: $"Başarısız giriş denemesi: {Input.Email}",
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
                    ModelState.AddModelError(string.Empty, "Giriş başarısız. E-posta veya şifre hatalı.");
                    return Page();
                }
            }

            // Hata varsa sayfayı tekrar göster
            return Page();
        }
    }
}