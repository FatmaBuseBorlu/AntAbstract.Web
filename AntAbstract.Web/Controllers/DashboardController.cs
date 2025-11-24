using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // DİKKAT: Sadece giriş yapmış üyeler girebilir!
    public class DashboardController : Controller
    {
        private readonly UserManager<AppUser> _userManager;

        public DashboardController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // Buraya ileride istatistikleri (Bildiri sayısı vb.) göndereceğiz.
            ViewBag.UserName = user.FirstName + " " + user.LastName;

            return View();
        }
    }
}