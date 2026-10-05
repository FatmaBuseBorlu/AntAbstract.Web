// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using AntAbstract.Domain.Entities;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly EmailConfirmationSender _confirmationSender;

        public ResendEmailConfirmationModel(UserManager<AppUser> userManager, EmailConfirmationSender confirmationSender)
        {
            _userManager = userManager;
            _confirmationSender = confirmationSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Adresin kayıtlı olup olmadığı dışarıya belli edilmez: her durumda aynı mesaj.
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user != null && !await _userManager.IsEmailConfirmedAsync(user) &&
                !AccountAnonymizer.IsAnonymized(user))
            {
                await _confirmationSender.SendAsync(user, Url, Request.Scheme, returnUrl: null);
            }

            ModelState.AddModelError(string.Empty,
                System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? "If this address has an unconfirmed account, a new confirmation email has been sent. Please check your inbox."
                    : "Bu adrese ait doğrulanmamış bir hesap varsa yeni bir doğrulama e-postası gönderildi. Lütfen gelen kutunuzu kontrol edin.");
            return Page();
        }
    }
}
