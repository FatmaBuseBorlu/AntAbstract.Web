using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AntAbstract.Web.Controllers
{
    // Sadece Admin ve Organizator rolündekiler erişebilir
    [Authorize(Roles = "Admin,Organizator")]
    public class ReviewerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;

        public ReviewerController(AppDbContext context,
                                  TenantContext tenantContext,
                                  UserManager<AppUser> userManager)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
        }

        // GET: Reviewer/Index
        //  GÜNCELLENMİŞ VE DÜZELTİLMİŞ METOT
        public async Task<IActionResult> Index()
        {
            // Tenant kontrolü (URL'de /slug olmalı)
            if (_tenantContext.Current == null)
            {
                return RedirectToAction("Index", "Home"); // Ana sayfaya yönlendir
            }

            // 1. Bu Tenant'a ait olan Konferansı bul
            var currentConference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.TenantId == _tenantContext.Current.Id);

            // Eğer bu slug'a ait bir konferans veritabanında yoksa, hata göster.
            if (currentConference == null)
            {
                ViewBag.ErrorMessage = "Bu kongre için henüz bir konferans detayı oluşturulmamış.";
                return View(new List<Reviewer>()); // Boş bir model ile View'ı göster
            }

            //  DÜZELTME: Konferans ID'sini ViewBag'e direkt atıyoruz.
            ViewBag.CurrentConferenceId = currentConference.Id;

            // 2. Bu Konferansa atanmış hakemleri al
            var assignedReviewers = await _context.Reviewers
                .Where(r => r.ConferenceId == currentConference.Id) // Doğru ID ile sorgulama
                .Include(r => r.AppUser) // Kullanıcı bilgilerini yükle
                .ToListAsync();

            // 3. Sisteme kayıtlı tüm kullanıcıları al (Dropdown için)
            var allUsers = await _userManager.Users.ToListAsync();
            ViewBag.AllUsers = new SelectList(allUsers, "Id", "Email");

            // ❌ ESKİ HATALI KOD KALDIRILDI
            // ViewBag.ConferenceId = new SelectList(...);

            return View(assignedReviewers);
        }

        // POST: Reviewer/Assign
        // Bu metot doğru çalışıyor, değişikliğe gerek yok.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(string userId, Guid conferenceId)
        {
            if (string.IsNullOrEmpty(userId) || conferenceId == Guid.Empty)
            {
                ModelState.AddModelError("", "Kullanıcı veya Kongre seçimi boş olamaz.");
                return RedirectToAction(nameof(Index)); // Hata durumunda Index'e dön
            }

            // 1. Kullanıcıya "Reviewer" Rolü Atama
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !await _userManager.IsInRoleAsync(user, "Reviewer"))
            {
                await _userManager.AddToRoleAsync(user, "Reviewer");
            }

            // 2. Reviewer Tablosuna Kayıt Ekleme (Bu Kongre için)
            var existingReviewer = await _context.Reviewers
                .FirstOrDefaultAsync(r => r.AppUserId == userId && r.ConferenceId == conferenceId);

            if (existingReviewer == null)
            {
                var reviewer = new Reviewer
                {
                    Id = Guid.NewGuid(), // ID'yi burada oluşturmak daha güvenli
                    AppUserId = userId,
                    ConferenceId = conferenceId,
                    IsActive = true // Yeni atanan hakem aktif
                };
                _context.Reviewers.Add(reviewer);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}