using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin,Organizator")]
    public class AssignmentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly IEmailService _emailService;
        private readonly IReviewerRecommendationService _recommendationService;
        private readonly UserManager<AppUser> _userManager;

        public AssignmentController(AppDbContext context,
                                            TenantContext tenantContext,
                                            IEmailService emailService,
                                            UserManager<AppUser> userManager, 
                                            IReviewerRecommendationService recommendationService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _emailService = emailService;
            _userManager = userManager;
            _recommendationService = recommendationService;
        }
        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current == null)
            {
                TempData["ErrorMessage"] = "Lütfen bir kongre URL'i (/slug) üzerinden işlem yapın.";
                return RedirectToAction("Index", "Home");
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            if (conference == null)
            {
                ViewBag.ErrorMessage = "Bu kongre için veritabanında bir konferans kaydı bulunamadı.";
                return View(new List<Domain.Entities.Submission>());
            }

            var submissions = await _context.Submissions
                .Where(s => s.ConferenceId == conference.Id)
                .Include(s => s.Author)
                .ToListAsync();

            return View(submissions);
        }

        public async Task<IActionResult> Assign(Guid id)
        {
            var submission = await _context.Submissions.Include(s => s.Author).FirstOrDefaultAsync(s => s.SubmissionId == id);
            if (submission == null) return NotFound();

            var recommendedReviewers = await _recommendationService.GetRecommendationsAsync(id);
            var allReviewers = await _userManager.GetUsersInRoleAsync("Reviewer");
            var allOtherReviewers = allReviewers.Except(recommendedReviewers).ToList();

            var viewModel = new AssignReviewerViewModel
            {
                Submission = submission,
                RecommendedReviewers = recommendedReviewers.ToList(),
                AllOtherReviewers = allOtherReviewers
            };

            return View(viewModel);
        }

       
     

    } 
}