using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Kayıttan sonra "e-postanızı kontrol edin" sayfası.
    ///
    /// Eskiden e-posta adresini sorgu dizesinden alıyor, kayıtlı değilse 404
    /// dönüyor (adres sistemde var mı sorusuna cevap veriyordu) ve doğrulama
    /// bağlantısını sayfada gösteriyordu — e-postaya erişmeden doğrulamak
    /// mümkündü. Adres artık yalnızca kayıt adımından TempData ile gelir.
    /// </summary>
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        public string? Email { get; set; }

        public bool EmailFailed { get; set; }

        public void OnGet()
        {
            Email = TempData["RegisteredEmail"] as string;
            EmailFailed = TempData["ConfirmationEmailFailed"] as bool? == true;
        }
    }
}
