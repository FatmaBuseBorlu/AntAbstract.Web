using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic; // List için eklendi
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class DecisionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public DecisionController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: Decision/Index
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Konferans bulunamadı.";
                return View(new List<Domain.Entities.Submission>());
            }

            // TÜM atamaları "Değerlendirildi" durumunda olan ve henüz nihai kararı verilmemiş özetleri getirir.
            var submissionsAwaitingDecision = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.ReviewAssignments)
                .Where(s => s.ConferenceId == conference.Id &&
                             s.FinalDecision == null && // Henüz kararı verilmemiş olanlar
                             s.ReviewAssignments.Any() &&
                             s.ReviewAssignments.All(ra => ra.Status == "Değerlendirildi"))
                .ToListAsync();

            return View(submissionsAwaitingDecision);
        }
    }
}