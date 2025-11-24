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

        // TenantContext bağımlılığı KALDIRILDI.
        public DashboardController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // SORGULAMA MANTIĞI: Sadece AuthorId'ye göre filtrele. 
            // Kullanıcı, katıldığı tüm kongrelerdeki bildirilerini görür.
            var submissions = _context.Submissions
                .Where(s => s.AuthorId == user.Id);

            // View Modelini doldur
            var viewModel = new DashboardViewModel
            {
                TotalSubmissions = await submissions.CountAsync(),
                AcceptedSubmissions = await submissions.CountAsync(s => s.Status == SubmissionStatus.Accepted),
                AwaitingDecision = await submissions.CountAsync(s => s.Status == SubmissionStatus.UnderReview || s.Status == SubmissionStatus.New)
            };

            return View(viewModel);
        }

        // ... Diğer dashboard action'ları buraya eklenecektir ...
    }
}