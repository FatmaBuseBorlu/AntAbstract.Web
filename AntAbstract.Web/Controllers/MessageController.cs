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
    [Authorize] 
    public class MessageController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public MessageController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Inbox()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var messages = await _context.Messages
                .Include(m => m.Sender) 
                .Where(m => m.ReceiverId == userId)
                .OrderByDescending(m => m.SentDate)
                .ToListAsync();

            return View(messages);
        }

        public async Task<IActionResult> Outbox()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var messages = await _context.Messages
                .Include(m => m.Receiver) 
                .Where(m => m.SenderId == userId)
                .OrderByDescending(m => m.SentDate)
                .ToListAsync();

            return View(messages);
        }

        public async Task<IActionResult> Create()
        {
           
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var users = await _userManager.Users
                .Where(u => u.Id != currentUserId)
                .ToListAsync();

            ViewBag.Users = new SelectList(users, "Id", "Email");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection collection)
        {
            try
            {
                string receiverId = collection["ReceiverId"];
                string subject = collection["Subject"];
                string content = collection["Content"];

                if (string.IsNullOrEmpty(receiverId) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(content))
                {
                    ModelState.AddModelError("", "Alıcı, Konu ve Mesaj alanları boş bırakılamaz.");
                    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var users = await _userManager.Users.Where(u => u.Id != currentUserId).ToListAsync();
                    ViewBag.Users = new SelectList(users, "Id", "Email");
                    return View();
                }
                var newMessage = new Message
                {
                    SenderId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    ReceiverId = receiverId,
                    Subject = subject,
                    Content = content,
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Add(newMessage);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"]= "Mesajınız başarıyla gönderildi.";
                return RedirectToAction(nameof(Inbox));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Mesaj gönderilirken bir hata oluştu.";
                return RedirectToAction(nameof(Create));
            }
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver) 
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null)
            {
                return NotFound();
            }

            if (message.ReceiverId != currentUserId && message.SenderId != currentUserId)
            {
                return Forbid(); 
            }

            if (message.ReceiverId == currentUserId && !message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(message);
        }
    }
}