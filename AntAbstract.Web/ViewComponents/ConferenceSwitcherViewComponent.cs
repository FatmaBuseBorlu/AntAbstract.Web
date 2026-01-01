using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.ViewComponents
{
    public class ConferenceSwitcherViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;

        public ConferenceSwitcherViewComponent(AppDbContext context, UserManager<AppUser> userManager, TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        private async Task<bool> IsAdminLikeAsync(AppUser user)
        {
            return await _userManager.IsInRoleAsync(user, "Admin")
                || await _userManager.IsInRoleAsync(user, "Organizator");
        }

        private string GetSlug()
        {
            return RouteData.Values["slug"]?.ToString()
                   ?? _tenantContext.Current?.Slug
                   ?? HttpContext.Session.GetString("SelectedConferenceSlug")
                   ?? "";
        }

        private Guid? GetSelectedConferenceId()
        {
            string? confIdStr = null;

            if (_tenantContext.Current != null)
            {
                var tenantKey = $"SelectedConferenceId:{_tenantContext.Current.Id}";
                confIdStr = HttpContext.Session.GetString(tenantKey);
            }

            confIdStr ??= HttpContext.Session.GetString("SelectedConferenceId");

            return Guid.TryParse(confIdStr, out var parsedId) ? parsedId : null;
        }

        private IQueryable<Guid> GetUserConferenceIds(string userId)
        {
            var regIds = _context.Registrations
                .AsNoTracking()
                .Where(r => r.AppUserId == userId)
                .Select(r => r.ConferenceId);

            var submissionIds = _context.Submissions
                .AsNoTracking()
                .Where(s => s.AuthorId == userId)
                .Select(s => s.ConferenceId);

            var reviewIds = _context.ReviewAssignments
                .AsNoTracking()
                .Where(ra => ra.ReviewerId == userId)
                .Select(ra => ra.Submission.ConferenceId);

            return regIds.Union(submissionIds).Union(reviewIds);
        }

        private Task<System.Collections.Generic.List<Conference>> GetUserConferencesAsync(string userId)
        {
            var ids = GetUserConferenceIds(userId);

            return _context.Conferences
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync((System.Security.Claims.ClaimsPrincipal)User);
            if (user == null)
                return View("Default", new ConferenceSwitcherModel());

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isOrganizator = await _userManager.IsInRoleAsync(user, "Organizator");

            var slug = GetSlug();
            var selectedConferenceId = GetSelectedConferenceId();

            List<Conference> conferences;

            if (isAdmin)
            {
                conferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .OrderByDescending(c => c.StartDate)
                    .ToListAsync();
            }
            else if (isOrganizator)
            {
                var q = _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .OrderByDescending(c => c.StartDate)
                    .AsQueryable();

                if (_tenantContext.Current != null)
                    q = q.Where(c => c.TenantId == _tenantContext.Current.Id);
                else if (!string.IsNullOrWhiteSpace(slug))
                    q = q.Where(c => c.Tenant != null && c.Tenant.Slug == slug);

                conferences = await q.ToListAsync();
            }
            else
            {
                conferences = await GetUserConferencesAsync(user.Id);
            }

            string? currentConferenceName = null;

            if (selectedConferenceId.HasValue)
            {
                currentConferenceName = conferences
                    .Where(x => x.Id == selectedConferenceId.Value)
                    .Select(x => x.Title)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(currentConferenceName))
                {
                    currentConferenceName = await _context.Conferences
                        .AsNoTracking()
                        .Where(x => x.Id == selectedConferenceId.Value)
                        .Select(x => x.Title)
                        .FirstOrDefaultAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(currentConferenceName) && _tenantContext.Current != null)
                currentConferenceName = _tenantContext.Current.Name;

            var returnUrl = $"{Request.Path}{Request.QueryString}";

            var model = new ConferenceSwitcherModel
            {
                Conferences = conferences ?? new List<Conference>(),
                SelectedConferenceId = selectedConferenceId,
                CurrentConferenceName = currentConferenceName,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Dashboard" : returnUrl
            };

            return View("Default", model);
        }

    }
}
