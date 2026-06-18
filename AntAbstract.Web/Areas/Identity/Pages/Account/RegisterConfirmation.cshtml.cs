using System.Text;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<RegisterConfirmationModel> _localizer;

        public RegisterConfirmationModel(
            UserManager<AppUser> userManager,
            IStringLocalizer<RegisterConfirmationModel> localizer)
        {
            _userManager = userManager;
            _localizer = localizer;
        }

        public string Email { get; set; } = string.Empty;

        public bool DisplayConfirmAccountLink { get; set; }

        public string EmailConfirmationUrl { get; set; } = string.Empty;

        [TempData]
        public string StatusMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string email, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToPage("/Index");
            }

            returnUrl ??= Url.Content("~/");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", email]);
            }

            Email = email;

            DisplayConfirmAccountLink = true;

            if (DisplayConfirmAccountLink)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                EmailConfirmationUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme)!;
            }

            StatusMessage = _localizer["RegisterConfirmationInfo"];

            return Page();
        }
    }
}
