using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using AntAbstract.Web.Models.ViewModels.Admin.Submissions;
using AntAbstract.Web.Models.ViewModels.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;

        public SubmissionsController(
            AppDbContext context,
            ISubmissionService submissionService,
            IReviewService reviewService,
            UserManager<AppUser> userManager,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService)
        {
            _context = context;
            _submissionService = submissionService;
            _reviewService = reviewService;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
        }

        [HttpGet("/Admin/Submissions")]
        public async Task<IActionResult> SelectConference(Guid? conferenceId = null, string? returnUrl = null)
        {
            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                _selectedConferenceService.SetSelectedConferenceId(conferenceId.Value);

            var selectedId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedId != null)
            {
                var conf = await _context.Conferences
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (conf?.Tenant?.Slug != null)
                {
                    HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return LocalRedirect(returnUrl);

                    return RedirectToAction(nameof(Index), new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!isAdmin && user?.TenantId != null)
            {
                query = query.Where(c => c.TenantId == user.TenantId.Value);
            }
            else if (!isAdmin && user?.TenantId == null)
            {
                query = query.Where(c => false);
            }

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var vm = new SelectConferenceViewModel
            {
                Title = "Tüm Başvurular",
                Lead = "Başvuruları görüntülemek için önce kongre seçin.",
                PostUrl = "/Admin/Submissions/Select",
                SubmitText = "Başvuruları Görüntüle",
                Conferences = conferences,
                ReturnUrl = returnUrl
            };

            return View("~/Areas/Admin/Views/Shared/SelectConference.cshtml", vm);
        }

        [HttpPost("/Admin/Submissions/Select")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectConferencePost(Guid conferenceId, string? returnUrl = null)
        {
            var conf = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conf == null || conf.Tenant == null || string.IsNullOrWhiteSpace(conf.Tenant.Slug))
            {
                TempData["ErrorMessage"] = "Kongre bulunamadı.";
                return RedirectToAction(nameof(SelectConference));
            }

            _selectedConferenceService.SetSelectedConferenceId(conf.Id);
            HttpContext.Session.SetString("SelectedConferenceSlug", conf.Tenant.Slug);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index), new { slug = conf.Tenant.Slug, conferenceId = conf.Id });
        }

        [HttpGet("/{slug}/Admin/Submissions")]
        public async Task<IActionResult> Index(string slug, Guid? conferenceId = null, string? search = null, string? status = null)
        {
            if (_tenantContext.Current == null || !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Submissions" });

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
                _selectedConferenceService.SetSelectedConferenceId(conferenceId.Value);

            var selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            if (selectedConferenceId == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Submissions" });

            var conference = await _context.Conferences
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value && c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
                return RedirectToAction(nameof(SelectConference), new { returnUrl = $"/{slug}/Admin/Submissions" });

            var confId = conference.Id;

            var query = _context.Submissions
                .AsNoTracking()
                .Include(s => s.Conference)
                .Include(s => s.Author)
                .Where(x => x.ConferenceId == confId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(x =>
                    (x.Title != null && x.Title.Contains(s)) ||
                    (x.Author != null && (
                        (x.Author.FirstName != null && x.Author.FirstName.Contains(s)) ||
                        (x.Author.LastName != null && x.Author.LastName.Contains(s)) ||
                        (x.Author.Email != null && x.Author.Email.Contains(s))
                    ))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubmissionStatus>(status, out var parsed))
                query = query.Where(x => x.Status == parsed);

            var items = await query
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new AdminSubmissionRowModel
                {
                    Id = x.Id,
                    Title = x.Title ?? "",
                    AuthorName = x.Author == null ? "" : ((x.Author.FirstName ?? "") + " " + (x.Author.LastName ?? "")).Trim(),
                    ConferenceTitle = x.Conference == null ? "" : (x.Conference.Title ?? ""),
                    CreatedAt = x.CreatedDate,
                    Status = x.Status.ToString()
                })
                .ToListAsync();

            var model = new AdminSubmissionsIndexModel
            {
                Slug = slug,
                ConferenceId = confId,
                ConferenceTitle = conference.Title,
                Search = search,
                Status = status,
                Items = items
            };

            return View("~/Areas/Admin/Views/Submissions/Index.cshtml", model);
        }

        [HttpGet("/Admin/Submissions/Details/{id}")]
        [HttpGet("/{slug}/Admin/Submissions/Details/{id}")]
        public async Task<IActionResult> Details(Guid id, string? slug = null, string? returnUrl = null)
        {
            var submission = await _submissionService.GetSubmissionByIdAsync(id);
            if (submission == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                var isAuthorized = await _context.Submissions
                    .Include(s => s.Conference)
                    .AnyAsync(s => s.Id == id && s.Conference.TenantId == user.TenantId);

                if (!isAuthorized)
                {
                    TempData["ErrorMessage"] = "Yetkisiz Erişim! Başka bir kuruma ait bildiriyi görüntüleyemezsiniz.";
                    return RedirectToAction(nameof(Index), new { slug = slug ?? _tenantContext.Current?.Slug });
                }
            }

            ViewBag.Referees = await _userManager.GetUsersInRoleAsync("Referee");
            ViewBag.Reviews = await _reviewService.GetReviewsBySubmissionIdAsync(id);

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
                ? returnUrl
                : $"{Request.PathBase}{Request.Path}{Request.QueryString}";

            if (string.IsNullOrWhiteSpace(effectiveReturnUrl))
                effectiveReturnUrl = string.IsNullOrWhiteSpace(slug) ? "/Admin/Submissions" : $"/{slug}/Admin/Submissions";

            ViewBag.ReturnUrl = effectiveReturnUrl;

            return View("~/Areas/Admin/Views/Submissions/Details.cshtml", submission);
        }

        [HttpPost("/Admin/Submissions/ChangeStatus")]
        [HttpPost("/{slug}/Admin/Submissions/ChangeStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(Guid id, string status, string? slug = null, string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                var isAuthorized = await _context.Submissions
                    .Include(s => s.Conference)
                    .AnyAsync(s => s.Id == id && s.Conference.TenantId == user.TenantId);

                if (!isAuthorized)
                {
                    TempData["ErrorMessage"] = "Yetkisiz İşlem! Başka bir kuruma ait bildirinin durumunu değiştiremezsiniz.";
                    return RedirectToAction(nameof(Index), new { slug = slug ?? _tenantContext.Current?.Slug });
                }
            }

            if (Enum.TryParse<SubmissionStatus>(status, out var newStatus))
            {
                await _submissionService.UpdateStatusAsync(id, newStatus);
                TempData["SuccessMessage"] = "Bildiri durumu başarıyla güncellendi: " + status;
            }
            else
            {
                TempData["ErrorMessage"] = "Geçersiz durum bilgisi.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (!string.IsNullOrWhiteSpace(slug))
                return RedirectToAction(nameof(Index), new { slug });

            return RedirectToAction(nameof(SelectConference));
        }

        [HttpPost("/Admin/Submissions/Delete")]
        [HttpPost("/{slug}/Admin/Submissions/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id, string? slug = null, string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                var isAuthorized = await _context.Submissions
                    .Include(s => s.Conference)
                    .AnyAsync(s => s.Id == id && s.Conference.TenantId == user.TenantId);

                if (!isAuthorized)
                {
                    TempData["ErrorMessage"] = "Yetkisiz İşlem! Başka bir kuruma ait bildiriyi silemezsiniz.";
                    return RedirectToAction(nameof(Index), new { slug = slug ?? _tenantContext.Current?.Slug });
                }
            }

            await _submissionService.DeleteSubmissionAsync(id);
            TempData["SuccessMessage"] = "Bildiri başarıyla silindi.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (!string.IsNullOrWhiteSpace(slug))
                return RedirectToAction(nameof(Index), new { slug });

            return RedirectToAction(nameof(SelectConference));
        }
    }
}