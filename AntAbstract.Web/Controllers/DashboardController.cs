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
        // Eğer Tenant bazlı filtreleme gerekiyorsa bunu da kullanırız, şimdilik yorum satırı:
        // private readonly TenantContext _tenantContext; 

        public DashboardController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // SADELEŞTİRİLMİŞ SORGULAMA: Sadece yazar ID'sine göre filtrelenir.
            var submissions = _context.Submissions
                .Where(s => s.AuthorId == user.Id);

            // YENİ VIEW MODEL OLUŞTURUYORUZ
            var viewModel = new DashboardViewModel
            {
                TotalSubmissions = await submissions.CountAsync(),

                // Status alanında problem yok, hepsi Enum ile karşılaştırılıyor.
                AcceptedSubmissions = await submissions.CountAsync(s => s.Status == SubmissionStatus.Accepted),
                AwaitingDecision = await submissions.CountAsync(s => s.Status == SubmissionStatus.UnderReview || s.Status == SubmissionStatus.New)
            };

            return View(viewModel); // View'a bu modeli gönderiyoruz
        }
    }
}