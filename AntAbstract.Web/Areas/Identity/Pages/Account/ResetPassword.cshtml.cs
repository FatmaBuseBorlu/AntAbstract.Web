// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using AntAbstract.Domain.Entities;
using AntAbstract.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [EnableRateLimiting("auth")]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IAuditService _audit;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(
            UserManager<AppUser> userManager,
            IAuditService audit,
            ILogger<ResetPasswordModel> logger)
        {
            _userManager = userManager;
            _audit = audit;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "Şifre en az {2}, en fazla {1} karakter olmalıdır.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Şifre onayı")]
            [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        public IActionResult OnGet(string code = null, string email = null)
        {
            if (code == null)
                return BadRequest("Şifre sıfırlama kodu eksik.");

            Input = new InputModel
            {
                Code = DecodeResetCode(code),
                Email = email ?? string.Empty
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Kullanıcı bulunamadı — enum attack'ı önlemek için aynı sayfaya yönlendir
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                _logger.LogInformation("Kullanıcı şifresini sıfırladı. UserId={UserId}", user.Id);

                await _audit.LogAsync(
                    category: "Auth",
                    action: "PasswordReset",
                    userId: user.Id,
                    userName: $"{user.FirstName} {user.LastName}".Trim(),
                    entityType: "AppUser",
                    entityId: user.Id,
                    description: $"{user.Email} kullanıcısı şifresini sıfırladı.",
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            // Başarısız deneme — brute-force bilgisi için logla
            _logger.LogWarning(
                "Şifre sıfırlama başarısız. Email={Email} IP={IP}",
                Input.Email,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return Page();
        }

        private static string DecodeResetCode(string code)
        {
            try
            {
                return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return code;
            }
        }
    }
}
