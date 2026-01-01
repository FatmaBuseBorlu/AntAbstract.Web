using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Author")]
    public class SubmissionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;

        public SubmissionController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
        }

        [HttpGet("/{slug}/Submission/Index")]
        public async Task<IActionResult> Index(string slug)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("MyConferences", "Dashboard", new { slug });

            var confId = _selectedConferenceService.GetSelectedConferenceId();
            if (confId == null)
                return RedirectToAction("MyConferences", "Dashboard", new { slug });

            var user = await _userManager.GetUserAsync(User);

            var list = await _context.Submissions
                .AsNoTracking()
                .Where(x => x.ConferenceId == confId.Value && x.AuthorId == user.Id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(list);
        }

        [HttpGet("/{slug}/Submission/Create")]
        public async Task<IActionResult> Create(string slug)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("MyConferences", "Dashboard", new { slug });

            var confId = _selectedConferenceService.GetSelectedConferenceId();
            if (confId == null)
            {
                TempData["ErrorMessage"] = "Özet göndermek için önce bir kongre seçin.";
                return RedirectToAction("MyConferences", "Dashboard", new { slug });
            }

            // burada senin Create viewmodel’in neyse onu dönersin
            return View();
        }

        [HttpPost("/{slug}/Submission/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(string slug /*, CreateSubmissionViewModel model */)
        {
            // aynı kontroller + save
            return RedirectToAction(nameof(Index), new { slug });
        }
    }
}
