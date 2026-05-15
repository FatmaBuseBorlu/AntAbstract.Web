using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AntAbstract.Web.Controllers
{
    public class LanguageController : Controller
    {
        private static readonly string[] SupportedCultures =
        {
            "tr-TR",
            "en-US"
        };

        [HttpPost("/Language/ChangeLanguage")]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeLanguage(string culture, string returnUrl)
        {
            if (!SupportedCultures.Contains(culture))
            {
                culture = "tr-TR";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect("/");
        }
    }
}