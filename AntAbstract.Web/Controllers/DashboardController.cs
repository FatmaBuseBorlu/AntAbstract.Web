using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // Bu sayfayı sadece giriş yapmış kullanıcılar görebilir
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;

        public DashboardController(AppDbContext context, UserManager<AppUser> userManager, TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            var roles = await _userManager.GetRolesAsync(user);

            var viewModel = new DashboardViewModel();

            // Yazar Paneli Verileri
            viewModel.MySubmissions = await _context.Submissions
                .Where(s => s.AuthorId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(5) // Son 5 özeti göster
                .ToListAsync();

            // Hakem Paneli Verileri
            if (roles.Contains("Reviewer"))
            {
                viewModel.MyReviewAssignments = await _context.ReviewAssignments
                    .Include(ra => ra.Submission)
                    .Where(ra => ra.Reviewer.AppUserId == userId && ra.Status == "Atandı")
                    .OrderBy(ra => ra.DueDate)
                    .Take(5) // Yaklaşan 5 görevi göster
                    .ToListAsync();
            }

            // Organizatör Paneli Verileri
            if (roles.Contains("Admin") || roles.Contains("Organizator"))
            {
                if (_tenantContext.Current != null)
                {
                    var conference = await _context.Conferences
                        .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

                    if (conference != null)
                    {
                        // Henüz hiçbir hakeme atanmamış özetlerin sayısı
                        viewModel.SubmissionsAwaitingAssignment = await _context.Submissions
                            .Where(s => s.ConferenceId == conference.Id && !s.ReviewAssignments.Any())
                            .CountAsync();

                        // Değerlendirmesi bitmiş, karar bekleyen özetlerin sayısı
                        viewModel.SubmissionsAwaitingDecision = await _context.Submissions
                            .Where(s => s.ConferenceId == conference.Id && s.FinalDecision == null && s.ReviewAssignments.Any() && s.ReviewAssignments.All(ra => ra.Status == "Değerlendirildi"))
                            .CountAsync();
                    }
                }
            }

            return View(viewModel);
        }
    }
}