using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize] // Sadece giriş yapmış kullanıcılar erişebilir
    public class MessageController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public MessageController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Message/Inbox
        // Kullanıcının Gelen Kutusunu listeler
        public async Task<IActionResult> Inbox()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var messages = await _context.Messages
                .Include(m => m.Sender) // Gönderen bilgisini de yükle
                .Where(m => m.ReceiverId == userId)
                .OrderByDescending(m => m.SentDate)
                .ToListAsync();

            return View(messages);
        }
        // GET: Message/Outbox
        // Kullanıcının Giden Kutusunu listeler
        public async Task<IActionResult> Outbox()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var messages = await _context.Messages
                .Include(m => m.Receiver) // Alıcı bilgisini de yükle
                .Where(m => m.SenderId == userId)
                .OrderByDescending(m => m.SentDate)
                .ToListAsync();

            return View(messages);
        }

        // GET: Message/Create
        // Yeni mesaj oluşturma formunu gösterir
        public async Task<IActionResult> Create()
        {
            // Mesaj gönderilebilecek tüm kullanıcıları listele (kendimiz hariç)
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var users = await _userManager.Users
                .Where(u => u.Id != currentUserId)
                .ToListAsync();

            ViewBag.Users = new SelectList(users, "Id", "Email");
            return View();
        }

        // POST: Message/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection collection)
        {
            try
            {
                // 1. Veriyi formdan manuel olarak oku
                string receiverId = collection["ReceiverId"];
                string subject = collection["Subject"];
                string content = collection["Content"];

                // 2. Basit bir kontrol yap
                if (string.IsNullOrEmpty(receiverId) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(content))
                {
                    ModelState.AddModelError("", "Alıcı, Konu ve Mesaj alanları boş bırakılamaz.");
                    // Hata durumunda kullanıcı listesini tekrar doldurup formu geri gönder
                    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var users = await _userManager.Users.Where(u => u.Id != currentUserId).ToListAsync();
                    ViewBag.Users = new SelectList(users, "Id", "Email");
                    return View();
                }

                // 3. Message nesnesini manuel olarak oluştur
                var newMessage = new Message
                {
                    SenderId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    ReceiverId = receiverId,
                    Subject = subject,
                    Content = content,
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                };

                // 4. Veritabanına kaydet
                _context.Add(newMessage);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"]= "Mesajınız başarıyla gönderildi.";
                return RedirectToAction(nameof(Inbox));
            }
            catch (Exception ex)
            {
                // Beklenmedik bir hata olursa...
                TempData["ErrorMessage"] = "Mesaj gönderilirken bir hata oluştu.";
                return RedirectToAction(nameof(Create));
            }
        }
        // GET: Message/Details/5
        // Bir mesajın detayını gösterir ve okundu olarak işaretler
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver) // Alıcıyı da yükle
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null)
            {
                return NotFound();
            }

            // Güvenlik Kontrolü: Kullanıcı ya gönderen ya da alıcı olmalı
            if (message.ReceiverId != currentUserId && message.SenderId != currentUserId)
            {
                return Forbid(); // Yetkisiz erişimi engelle
            }

            // Eğer mesajı okuyan kişi alıcı ise ve mesaj daha önce okunmadıysa,
            // "okundu" olarak işaretle ve kaydet
            if (message.ReceiverId == currentUserId && !message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(message);
        }
    }
}