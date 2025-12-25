using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    public class ProgramController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ProgramController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        [HttpGet("/{slug}/program")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null) return NotFound();

            var sessions = await _context.Sessions
                .Where(s => s.ConferenceId == _tenantContext.Current.Id)
                .Include(s => s.Submissions)
                    .ThenInclude(sub => sub.Author)
                .OrderBy(s => s.SessionDate) 
                .ToListAsync();

            ViewBag.ConferenceName = _tenantContext.Current.Name;

            return View(sessions);
        }

        [HttpGet("/{slug}/program/details/{id}")]
        public async Task<IActionResult> Details(string slug, System.Guid id)
        {
            var session = await _context.Sessions
               .Include(s => s.Submissions)
                   .ThenInclude(sub => sub.Author)
               .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null) return NotFound();

            return View(session);
        }
    }
}