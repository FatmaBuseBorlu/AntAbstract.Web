using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class ConferenceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public ConferenceController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _userManager = userManager;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Conference/Select")]
        public async Task<IActionResult> Select()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Redirect("/Identity/Account/Login");

            var isAdminOrOrg = User.IsInRole("Admin") || User.IsInRole("Organizator");

            IQueryable<Conference> query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant);

            if (!isAdminOrOrg)
            {
                var regIds = _context.Registrations
                    .AsNoTracking()
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId);

                var authorIds = _context.Submissions
                    .AsNoTracking()
                    .Where(s => s.AuthorId == user.Id)
                    .Select(s => s.ConferenceId);

                var reviewerIds = _context.ReviewAssignments
                    .AsNoTracking()
                    .Where(ra => ra.ReviewerId == user.Id)
                    .Join(_context.Submissions.AsNoTracking(),
                          ra => ra.SubmissionId,
                          s => s.Id,
                          (ra, s) => s.ConferenceId);

                var allowedIds = regIds
                    .Union(authorIds)
                    .Union(reviewerIds)
                    .Distinct();

                query = query.Where(c => allowedIds.Contains(c.Id));
            }

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Kongre Seç",
                Lead = "Devam etmek için bir kongre seçin.",
                PostUrl = "/Conference/Select",
                SubmitText = "Devam Et",
                Conferences = conferences
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Conference/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectPost(Guid conferenceId)
        {
            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return Redirect("/Conference/Select");
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conf.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            return Redirect($"/{conf.Tenant.Slug}/Dashboard?conferenceId={conf.Id}");
        }
    }
}
