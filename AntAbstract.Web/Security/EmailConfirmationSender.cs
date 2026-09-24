using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace AntAbstract.Web.Security
{
    /// <summary>
    /// Kayıt, harici (ORCID) kayıt ve "doğrulama e-postasını yeniden gönder"
    /// aynı e-postayı gönderir. Dil, isteğin arayüz diline göre seçilir.
    /// </summary>
    public class EmailConfirmationSender
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<EmailConfirmationSender> _logger;

        public EmailConfirmationSender(
            UserManager<AppUser> userManager,
            IEmailSender emailSender,
            ILogger<EmailConfirmationSender> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        /// <returns>E-posta kuyruğa/sunucuya teslim edildiyse true.</returns>
        public async Task<bool> SendAsync(AppUser user, IUrlHelper url, string scheme, string? returnUrl)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code, returnUrl },
                protocol: scheme);

            if (string.IsNullOrWhiteSpace(callbackUrl) || string.IsNullOrWhiteSpace(user.Email))
                return false;

            var en = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
            var link = HtmlEncoder.Default.Encode(callbackUrl);

            var subject = en ? "Confirm your email address" : "E-posta adresinizi doğrulayın";
            var heading = en ? "Welcome to AntAbstract" : "AntAbstract'a hoş geldiniz";
            var intro = en
                ? "Please confirm your email address to activate your account. You can sign in once it is confirmed."
                : "Hesabınızı etkinleştirmek için e-posta adresinizi doğrulayın. Doğruladıktan sonra giriş yapabilirsiniz.";
            var button = en ? "Confirm email" : "E-postamı doğrula";
            var ignore = en
                ? "If you did not create this account, you can ignore this email."
                : "Bu hesabı siz oluşturmadıysanız bu e-postayı yok sayabilirsiniz.";

            try
            {
                await _emailSender.SendEmailAsync(
                    user.Email,
                    subject,
                    $"""
                    <div style="font-family:Arial,sans-serif;line-height:1.6;color:#1f2937">
                        <h2 style="margin:0 0 12px;color:#0f172a">{heading}</h2>
                        <p>{intro}</p>
                        <p>
                            <a href="{link}"
                               style="display:inline-block;background:#0ea5e9;color:#ffffff;text-decoration:none;padding:12px 18px;border-radius:10px;font-weight:700">
                                {button}
                            </a>
                        </p>
                        <p style="color:#64748b;font-size:13px">{ignore}</p>
                    </div>
                    """);

                return true;
            }
            catch (Exception ex)
            {
                // Kayıt yine de tamamlanır; kullanıcı "yeniden gönder" ile tekrar deneyebilir.
                _logger.LogError(ex, "Doğrulama e-postası gönderilemedi. UserId={UserId}", user.Id);
                return false;
            }
        }
    }
}
