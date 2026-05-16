using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.ViewComponents
{
    public class ConferenceSwitcherViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly TenantContext _tenantContext;

        public ConferenceSwitcherViewComponent(
            AppDbContext context,
            UserManager<AppUser> userManager,
            TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
        }

        private string GetSlug()
        {
            return ViewContext.RouteData.Values["slug"]?.ToString()
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

            return Guid.TryParse(confIdStr, out var parsedId)
                ? parsedId
                : null;
        }

        private IQueryable<Guid> GetUserConferenceIds(string userId)
        {
            var registrationIds = _context.Registrations
                .AsNoTracking()
                .Where(r => r.AppUserId == userId)
                .Select(r => r.ConferenceId);

            var submissionIds = _context.Submissions
                .AsNoTracking()
                .Where(s => s.AuthorId == userId)
                .Select(s => s.ConferenceId);

            var reviewIds =
                from reviewAssignment in _context.ReviewAssignments.AsNoTracking()
                join submission in _context.Submissions.AsNoTracking()
                    on reviewAssignment.SubmissionId equals submission.Id
                where reviewAssignment.ReviewerId == userId
                select submission.ConferenceId;

            return registrationIds
                .Union(submissionIds)
                .Union(reviewIds)
                .Distinct();
        }

        private async Task<List<Conference>> GetUserConferencesAsync(string userId)
        {
            var conferenceIds = GetUserConferenceIds(userId);

            return await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c => conferenceIds.Contains(c.Id))
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
        }

        private async Task<List<Conference>> GetAdminConferencesAsync(AppUser user)
        {
            if (!user.TenantId.HasValue)
            {
                return new List<Conference>();
            }

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c => c.TenantId == user.TenantId.Value);

            if (_tenantContext.Current != null)
            {
                query = query.Where(c => c.TenantId == _tenantContext.Current.Id);
            }

            return await query
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);

            if (user == null)
            {
                return View("Default", new ConferenceSwitcherModel());
            }

            var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (isSuperAdmin)
            {
                return View("Default", new ConferenceSwitcherModel());
            }

            var selectedConferenceId = GetSelectedConferenceId();

            List<Conference> conferences;

            if (isAdmin)
            {
                conferences = await GetAdminConferencesAsync(user);
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
                    selectedConferenceId = null;
                }
            }

            if (string.IsNullOrWhiteSpace(currentConferenceName) &&
                _tenantContext.Current != null &&
                isAdmin)
            {
                currentConferenceName = _tenantContext.Current.Name;
            }

            var returnUrl = $"{Request.Path}{Request.QueryString}";

            var model = new ConferenceSwitcherModel
            {
                Conferences = conferences,
                SelectedConferenceId = selectedConferenceId,
                CurrentConferenceName = currentConferenceName,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? "/Dashboard"
                    : returnUrl
            };

            return View("Default", model);
        }
    }
}