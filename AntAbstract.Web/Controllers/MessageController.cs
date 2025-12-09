using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var inbox = await _context.Messages
                .Include(m => m.Sender)
                .Where(m => m.ReceiverId == user.Id && !m.IsDeleted)
                .OrderByDescending(m => m.SentDate) 
                .ToListAsync();

            var sent = await _context.Messages
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == user.Id && !m.IsDeleted) 
                .OrderByDescending(m => m.SentDate) 
                .ToListAsync();

            ViewBag.Inbox = inbox;
            ViewBag.Sent = sent;

            return View();
        }

        public async Task<IActionResult> Details(Guid id) 
        {
            var message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null) return NotFound();

            var userId = _userManager.GetUserId(User);

            if (message.ReceiverId != userId && message.SenderId != userId)
            {
                return Forbid();
            }

            if (message.ReceiverId == userId && !message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(message);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
           
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var adminUser = admins.FirstOrDefault();

            if (adminUser == null)
            {
                adminUser = await _userManager.Users.FirstOrDefaultAsync();
            }

            ViewBag.ReceiverId = adminUser?.Id;
            ViewBag.ReceiverName = "Kongre Yönetimi"; 

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string receiverId, string subject, string content)
        {
            if (string.IsNullOrEmpty(receiverId) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(content))
            {
                TempData["ErrorMessage"] = "Lütfen alıcı, konu ve mesaj alanlarını doldurunuz.";
                return RedirectToAction(nameof(Create));
            }

            var newMessage = new Message
            {
                SenderId = _userManager.GetUserId(User),
                ReceiverId = receiverId,
                Subject = subject,
                Content = content,
                SentDate = DateTime.UtcNow, 
                IsRead = false,
                IsDeleted = false
            };

            _context.Messages.Add(newMessage);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Mesajınız başarıyla gönderildi.";
            return RedirectToAction(nameof(Index)); 
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id) 
        {
            var message = await _context.Messages.FindAsync(id);
            var userId = _userManager.GetUserId(User);

            if (message != null)
            {
                if (message.ReceiverId == userId || message.SenderId == userId)
                {
                    message.IsDeleted = true;
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}