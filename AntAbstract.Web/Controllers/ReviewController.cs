using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Reviewer")] // Bu sayfa sadece Hakemler içindir.
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReviewController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Review
        // O anki hakeme atanmış tüm değerlendirme görevlerini listeler.
        public async Task<IActionResult> Index()
        {
            // O anki kullanıcının ID'sini al (bu bir string).
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Veritabanındaki ReviewAssignment.ReviewerId (artık string) ile eşleştir.
            var assignments = await _context.ReviewAssignments
                .Where(ra => ra.ReviewerId == currentUserId)
                .Include(ra => ra.Submission) // Özet bilgilerini de getir
                .OrderByDescending(ra => ra.AssignedDate)
                .ToListAsync();

            return View(assignments);
        }

        // Diğer metotlar (değerlendirme yapma, kaydetme vb.) buraya eklenebilir.
        // Şimdilik bu temel yapı, derleme hatalarını çözecektir.
    }
}