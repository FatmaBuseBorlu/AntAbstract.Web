using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class ReviewerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;

        public ReviewerController(AppDbContext context,
                                  TenantContext tenantContext,
                                  UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
            if (conference == null) return View(new System.Collections.Generic.List<Reviewer>());

            var reviewers = await _context.Reviewers
                .Where(r => r.ConferenceId == conference.Id)
                .Include(r => r.AppUser)
                .ToListAsync();

            return View(reviewers);
        }

        public async Task<IActionResult> Create()
        {
            var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
            var existingReviewerUserIds = await _context.Reviewers
                .Where(r => r.ConferenceId == conference.Id)
                .Select(r => r.AppUserId)
                .ToListAsync();

            var potentialReviewers = await _userManager.Users
                .Where(u => !existingReviewerUserIds.Contains(u.Id))
                .ToListAsync();

            ViewBag.AppUserId = new SelectList(potentialReviewers, "Id", "Email");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AppUserId,IsActive")] Reviewer reviewer)
        {
            var conference = await _context.Conferences.FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);
            reviewer.ConferenceId = conference.Id;

            ModelState.Remove(nameof(reviewer.Conference));
            ModelState.Remove(nameof(reviewer.AppUser));

            if (ModelState.IsValid)
            {
                _context.Add(reviewer);
                await _context.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(reviewer.AppUserId);
                if (user != null && !await _userManager.IsInRoleAsync(user, "Reviewer"))
                {
                    await _userManager.AddToRoleAsync(user, "Reviewer");
                }

                return RedirectToAction(nameof(Index));
            }
            var existingReviewerUserIds = await _context.Reviewers.Where(r => r.ConferenceId == conference.Id).Select(r => r.AppUserId).ToListAsync();
            var potentialReviewers = await _userManager.Users.Where(u => !existingReviewerUserIds.Contains(u.Id)).ToListAsync();
            ViewBag.AppUserId = new SelectList(potentialReviewers, "Id", "Email", reviewer.AppUserId);
            return View(reviewer);
        }

[HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(string userId, Guid conferenceId)
        {
            if (string.IsNullOrEmpty(userId) || conferenceId == Guid.Empty)
            {
                ModelState.AddModelError("", "Kullanıcı veya Kongre seçimi boş olamaz.");
                return RedirectToAction(nameof(Index)); 
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !await _userManager.IsInRoleAsync(user, "Reviewer"))
            {
                await _userManager.AddToRoleAsync(user, "Reviewer");
            }

            var existingReviewer = await _context.Reviewers
                .FirstOrDefaultAsync(r => r.AppUserId == userId && r.ConferenceId == conferenceId);

            if (existingReviewer == null)
            {
                var reviewer = new Reviewer
                {
                    Id = Guid.NewGuid(), 
                    AppUserId = userId,
                    ConferenceId = conferenceId,
                    IsActive = true 
                };
                _context.Reviewers.Add(reviewer);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var reviewer = await _context.Reviewers.FindAsync(id);
            if (reviewer != null)
            {
                reviewer.IsActive = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(Guid id)
        {
            var reviewer = await _context.Reviewers.FindAsync(id);
            if (reviewer != null)
            {
                reviewer.IsActive = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reviewer = await _context.Reviewers
                .Include(r => r.AppUser) 
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reviewer == null)
            {
                return NotFound();
            }
            return View(reviewer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, string expertiseAreas, bool isActive)
        {
            var reviewerToUpdate = await _context.Reviewers.FindAsync(id);
            if (reviewerToUpdate == null)
            {
                return NotFound();
            }

           
            reviewerToUpdate.ExpertiseAreas = expertiseAreas;
            reviewerToUpdate.IsActive = isActive;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Reviewers.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}