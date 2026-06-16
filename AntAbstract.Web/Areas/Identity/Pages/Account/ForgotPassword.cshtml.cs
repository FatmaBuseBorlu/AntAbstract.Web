using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [EnableRateLimiting("auth")]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IAuditService _audit;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<AppUser> userManager,
            IEmailSender emailSender,
            IAuditService audit,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _audit = audit;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "E-posta adresi zorunludur.")]
            [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
            [Display(Name = "E-posta")]
            public string Email { get; set; } = string.Empty;
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

            var user = await _userManager.FindByEmailAsync(Input.Email);

            /*
                Güvenlik için:
                Kullanıcı yoksa bile hata göstermiyoruz.
                Çünkü "bu e-posta sistemde var mı yok mu" bilgisini dışarıya vermemek gerekir.
            */
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    code
                },
                protocol: Request.Scheme);

            if (string.IsNullOrWhiteSpace(callbackUrl))
            {
                ModelState.AddModelError(string.Empty, "Şifre sıfırlama bağlantısı oluşturulamadı.");
                return Page();
            }

            await _emailSender.SendEmailAsync(
                Input.Email,
                "Şifrenizi sıfırlayın",
                $"Şifrenizi sıfırlamak için <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>buraya tıklayın</a>.");

            _logger.LogInformation(
                "Şifre sıfırlama bağlantısı gönderildi. UserId={UserId} IP={IP}",
                user.Id, HttpContext.Connection.RemoteIpAddress?.ToString());

            await _audit.LogAsync(
                category: "Auth",
                action: "PasswordResetRequested",
                userId: user.Id,
                userName: $"{user.FirstName} {user.LastName}".Trim(),
                entityType: "AppUser",
                entityId: user.Id,
                description: $"{user.Email} için şifre sıfırlama e-postası gönderildi.",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}