using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Reviewer")] 
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReviewController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

  
        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var assignments = await _context.ReviewAssignments
                .Where(ra => ra.ReviewerId == currentUserId)
                .Include(ra => ra.Submission) 
                .OrderByDescending(ra => ra.AssignedDate)
                .ToListAsync();

            return View(assignments);
        }
        public async Task<IActionResult> PerformReview(Guid id) 
        {

            var assignment = await _context.ReviewAssignments
                .Include(a => a.Submission)
                .ThenInclude(s => s.Conference)
                .FirstOrDefaultAsync(a => a.ReviewAssignmentId == id);

            if (assignment == null) return NotFound();

            var reviewForm = await _context.ReviewForms
                .Include(f => f.Criteria) 
                .FirstOrDefaultAsync(f => f.ConferenceId == assignment.Submission.ConferenceId);

            if (reviewForm == null)
            {
                return Content("Bu kongre için henüz bir değerlendirme formu oluşturulmamıştır.");
            }

            var viewModel = new PerformReviewViewModel
            {
                Assignment = assignment,
                Criteria = reviewForm.Criteria.OrderBy(c => c.DisplayOrder).ToList(),
                Answers = new List<ReviewAnswer>() 
            };

            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PerformReview(PerformReviewViewModel viewModel)
        {
      
            var assignment = await _context.ReviewAssignments.FindAsync(viewModel.Assignment.ReviewAssignmentId);
            if (assignment == null)
            {
                return NotFound();
            }

            var newReview = new Review
            {
                ReviewAssignmentId = assignment.ReviewAssignmentId,
                ReviewDate = DateTime.UtcNow
            };
            _context.Reviews.Add(newReview);
            await _context.SaveChangesAsync();

            foreach (var answer in viewModel.Answers)
            {
           
                answer.ReviewId = newReview.Id;
                _context.ReviewAnswers.Add(answer);
            }

            assignment.Status = "Completed";
            _context.ReviewAssignments.Update(assignment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Değerlendirmeniz başarıyla gönderildi. Teşekkür ederiz!";
            return RedirectToAction("Index"); 
        }
    }
}