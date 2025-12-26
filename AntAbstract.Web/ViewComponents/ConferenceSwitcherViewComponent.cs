using System;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.ViewComponents
{
    public class ConferenceSwitcherViewComponent : ViewComponent
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ConferenceSwitcherViewComponent(
            UserManager<AppUser> userManager,
            AppDbContext context,
            TenantContext tenantContext)
        {
            _userManager = userManager;
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<IViewComponentResult> InvokeAsync(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
                return Content(string.Empty);

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin)
                return Content(string.Empty);

            var selectedConferenceId = GetSelectedConferenceIdFromSession();

            var conferences = await _context.Registrations
                .AsNoTracking()
                .Where(r => r.AppUserId == user.Id)
                .Include(r => r.Conference)
                .ThenInclude(c => c.Tenant)
                .Select(r => r.Conference)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            if (conferences == null || conferences.Count == 0)
                return Content(string.Empty);

            string? currentConferenceName = null;

            if (selectedConferenceId.HasValue)
            {
                currentConferenceName = await _context.Conferences
                    .AsNoTracking()
                    .Where(x => x.Id == selectedConferenceId.Value)
                    .Select(x => x.Title)
                    .FirstOrDefaultAsync();
            }

            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
                ? returnUrl
                : $"{HttpContext.Request.Path}{HttpContext.Request.QueryString}";

            var model = new ConferenceSwitcherModel(
                selectedConferenceId,
                currentConferenceName,
                string.IsNullOrWhiteSpace(effectiveReturnUrl) ? "/Dashboard" : effectiveReturnUrl,
                conferences.Select(c => new ConferenceSwitcherItemModel(
                    c.Id,
                    c.Title ?? "",
                    c.Tenant?.Slug ?? c.Slug ?? ""
                )).ToList()
            );

            return View(model);
        }

        private Guid? GetSelectedConferenceIdFromSession()
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
    }
}
