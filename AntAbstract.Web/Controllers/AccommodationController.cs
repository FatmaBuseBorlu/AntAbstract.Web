using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class AccommodationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AccommodationController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);


            var hotels = await _context.Hotels
                .Include(h => h.RoomTypes)
                .Include(h => h.Conference)
                .ToListAsync();

            return View(hotels);
        }

        public async Task<IActionResult> SeedData()
        {
            if (!await _context.Hotels.AnyAsync())
            {
                var conference = await _context.Conferences.FirstOrDefaultAsync();
                if (conference == null) return Content("Hata: Sistemde hiç kongre yok. Önce kongre eklemelisin.");

                var newHotel = new Hotel
                {
                    Id = Guid.NewGuid(),
                    Name = "Grand AntAbstract Hotel",
                    Description = "5 Yıldızlı, deniz manzaralı kongre oteli.",
                    Address = "Lara Caddesi, Antalya",
                    ConferenceId = conference.Id,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Hotels.Add(newHotel);
                await _context.SaveChangesAsync();

                var rooms = new List<RoomType>
                {
                    new RoomType { Name = "Tek Kişilik Oda", Price = 1500, Currency = "TL", Capacity = 1, TotalQuota = 50, HotelId = newHotel.Id },
                    new RoomType { Name = "Çift Kişilik Oda (Double)", Price = 2500, Currency = "TL", Capacity = 2, TotalQuota = 30, HotelId = newHotel.Id },
                    new RoomType { Name = "Deluxe Suite", Price = 5000, Currency = "TL", Capacity = 3, TotalQuota = 5, HotelId = newHotel.Id }
                };

                _context.RoomTypes.AddRange(rooms);
                await _context.SaveChangesAsync();

                return Content("Başarılı! Test oteli ve odaları eklendi. Şimdi /Accommodation sayfasına dönebilirsin.");
            }

            return Content("Zaten otel verisi var. Ekleme yapılmadı.");
        }
    }
}