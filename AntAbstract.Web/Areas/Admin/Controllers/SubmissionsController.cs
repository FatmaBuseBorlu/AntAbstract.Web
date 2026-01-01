using System;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator,Editor")]
    public class SubmissionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISubmissionService _submissionService;
        private readonly IReviewService _reviewService;
        private readonly UserManager<AppUser> _userManager;

        public SubmissionsController(
            AppDbContext context,
            ISubmissionService submissionService,
            IReviewService reviewService,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _submissionService = submissionService;
            _reviewService = reviewService;
            _userManager = userManager;
        }

        [HttpGet]
        [Route("Admin/Submissions")]
        [Route("{slug}/Admin/Submissions")]
        public async Task<IActionResult> Index(Guid? conferenceId = null, string? search = null, string? status = null)
        {
            var query = _context.Submissions
                .AsNoTracking()
                .Include(s => s.Conference)
                .Include(s => s.Author)
                .AsQueryable();

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                query = query.Where(x => x.ConferenceId == conferenceId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(x =>
                    (x.Title != null && x.Title.Contains(s)) ||
                    (x.Author != null && (
                        (x.Author.FirstName != null && x.Author.FirstName.Contains(s)) ||
                        (x.Author.LastName != null && x.Author.LastName.Contains(s)) ||
                        (x.Author.Email != null && x.Author.Email.Contains(s))
                    )) ||
                    (x.Conference != null && x.Conference.Title != null && x.Conference.Title.Contains(s))
                );
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<SubmissionStatus>(status, out var parsed))
                    query = query.Where(x => x.Status == parsed);
            }

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new AdminSubmissionRowModel
                {
                    Id = x.Id,
                    Title = x.Title ?? "",
                    AuthorName = x.Author == null ? "" : ((x.Author.FirstName ?? "") + " " + (x.Author.LastName ?? "")).Trim(),
                    ConferenceTitle = x.Conference == null ? "" : (x.Conference.Title ?? ""),
                    CreatedAt = x.CreatedAt,
                    Status = x.Status.ToString()
                })
                .ToListAsync();

            string? confTitle = null;
            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                confTitle = await _context.Conferences
                    .AsNoTracking()
                    .Where(c => c.Id == conferenceId.Value)
                    .Select(c => c.Title)
                    .FirstOrDefaultAsync();
            }

            var model = new AdminSubmissionsIndexModel
            {
                ConferenceId = conferenceId,
                ConferenceTitle = confTitle,
                Search = search,
                Status = status,
                Items = items
            };

            return View(model);
        }

        [HttpGet]
        [Route("Admin/Submissions/Details/{id}")]
        [Route("{slug}/Admin/Submissions/Details/{id}")]
        public async Task<IActionResult> Details(Guid id, string? returnUrl = null)
        {
            var submission = await _submissionService.GetSubmissionByIdAsync(id);
            if (submission == null)
                return NotFound();

            ViewBag.Referees = await _userManager.GetUsersInRoleAsync("Referee");
            ViewBag.Reviews = await _reviewService.GetReviewsBySubmissionIdAsync(id);

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
                ? returnUrl
                : $"{Request.PathBase}{Request.Path}{Request.QueryString}";

            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(effectiveReturnUrl) ? "/Admin/Submissions" : effectiveReturnUrl;

            return View(submission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Admin/Submissions/ChangeStatus")]
        [Route("{slug}/Admin/Submissions/ChangeStatus")]
        public async Task<IActionResult> ChangeStatus(Guid id, string status, string? returnUrl = null)
        {
            if (Enum.TryParse<SubmissionStatus>(status, out var newStatus))
            {
                await _submissionService.UpdateStatusAsync(id, newStatus);
                TempData["SuccessMessage"] = "Bildiri durumu güncellendi: " + status;
            }
            else
            {
                TempData["ErrorMessage"] = "Geçersiz durum bilgisi.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Admin/Submissions/Delete")]
        [Route("{slug}/Admin/Submissions/Delete")]
        public async Task<IActionResult> Delete(Guid id, string? returnUrl = null)
        {
            await _submissionService.DeleteSubmissionAsync(id);
            TempData["SuccessMessage"] = "Bildiri silindi.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}
