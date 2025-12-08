using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        private readonly TenantContext _tenantContext;

        public DashboardController(AppDbContext context, UserManager<AppUser> userManager, TenantContext tenantContext)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext; 
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var submissions = _context.Submissions
                .Where(s => s.AuthorId == user.Id);

      
            string currentConferenceName = "Genel Yönetim Paneli";

            if (_tenantContext.Current != null) 
            {
                currentConferenceName = _tenantContext.Current.Name;
            }
            var myConferences = await _context.Registrations
                .Where(r => r.AppUserId == user.Id)
                .Include(r => r.Conference) 
                .ThenInclude(c => c.Tenant) 
                .Select(r => r.Conference) 
                .OrderByDescending(c => c.StartDate) 
                .ToListAsync();

            var viewModel = new DashboardViewModel
            {
                TotalSubmissions = await submissions.CountAsync(),
                AcceptedSubmissions = await submissions.CountAsync(s => s.Status == SubmissionStatus.Accepted),
                AwaitingDecision = await submissions.CountAsync(s => s.Status == SubmissionStatus.UnderReview || s.Status == SubmissionStatus.New),
                ConferenceName = currentConferenceName,
                MyConferences = myConferences
            };

            return View(viewModel);
        }

        public async Task<IActionResult> MyConferences()
        {
            var user = await _userManager.GetUserAsync(User);

            var myConferences = await _context.Registrations
                .Where(r => r.AppUserId == user.Id)
                .Include(r => r.Conference)
                    .ThenInclude(c => c.Tenant)
                .OrderByDescending(r => r.RegistrationDate)
                .Select(r => r.Conference)
                .ToListAsync();

            return View(myConferences);
        }
    }
}