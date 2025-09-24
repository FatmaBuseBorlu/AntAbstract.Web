using AntAbstract.Infrastructure.Context;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // Bu satır çook önemli!
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;

        public SubmissionController(AppDbContext context, UserManager<AppUser> userManager, TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        // GET: Submission
        // Yazarın kendi gönderdiği özetleri listeler
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Giriş yapmış kullanıcının ID'si
            var submissions = await _context.Submissions
                                      .Where(s => s.AuthorId == userId)
                                      .ToListAsync();

            return View(submissions);
        }

        // GET: Submission/Create
        // Yeni özet gönderme formunu gösterir
        public IActionResult Create()
        {
            return View();
        }
    }
}