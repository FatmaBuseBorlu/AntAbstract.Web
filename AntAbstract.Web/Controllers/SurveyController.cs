using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class SurveyController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public SurveyController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(string q1, string q2, string q3, string q4, string q5)
        {
            var user = await _userManager.GetUserAsync(User);

            // 1. Yöneticiyi Bul
            var adminUser = (await _userManager.GetUsersInRoleAsync("Admin")).FirstOrDefault()
                            ?? await _userManager.Users.FirstOrDefaultAsync();

            // 2. Cevapları Okunaklı Bir Mesaj Haline Getir
            string surveyContent = $@"
                <p><strong>Kullanıcı:</strong> {user.FirstName} {user.LastName} ({user.Email})</p>
                <hr>
                <p><strong>1. Beklentileri karşıladı mı?</strong><br>{q1}</p>
                <p><strong>2. Tavsiye eder misiniz?</strong><br>{q2}</p>
                <p><strong>3. Network katkısı oldu mu?</strong><br>{q3}</p>
                <p><strong>4. Ne öğrendiniz?</strong><br>{q4}</p>
                <p><strong>5. Uygulama planınız nedir?</strong><br>{q5}</p>
            ";

            // 3. Mesaj Olarak Kaydet (Yöneticiye Gönder)
            var message = new Message
            {
                SenderId = user.Id,
                ReceiverId = adminUser.Id,
                Subject = "📋 Kongre Değerlendirme Anketi",
                Content = surveyContent, // HTML formatında kaydediyoruz
                SentDate = DateTime.UtcNow,
                IsRead = false,
                IsDeleted = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Değerli geri bildiriminiz için teşekkür ederiz! Mesajınız yöneticiye iletildi.";
            return RedirectToAction("Index", "Dashboard");
        }
    }
}