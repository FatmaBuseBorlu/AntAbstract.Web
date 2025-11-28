using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        public DashboardController(UserManager<AppUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. O anki kullanıcıyı bul
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // 2. Kullanıcının bildirilerini çek
            var userSubmissions = _context.Submissions.Where(s => s.AuthorId == user.Id);

            // 3. İstatistikleri Hesapla
            var model = new DashboardViewModel
            {
                TotalSubmissions = await userSubmissions.CountAsync(),

                // Durumu "Kabul Edildi" olanlar
                AcceptedSubmissions = await userSubmissions.CountAsync(s => s.Status == SubmissionStatus.Accepted),

                // Durumu "Yeni" veya "İnceleniyor" olanlar (Bekleyen)
                AwaitingDecision = await userSubmissions.CountAsync(s => s.Status == SubmissionStatus.New || s.Status == SubmissionStatus.UnderReview)
            };

            // 4. Veriyi View'a gönder
            return View(model);
        }
    }
}