using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace AntAbstract.Web.Controllers
{
    [Authorize]
    public class AccommodationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly ISelectedConferenceService _selectedConferenceService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IStringLocalizer<AccommodationController> _localizer;
        private readonly IWebHostEnvironment _env;

        public AccommodationController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<AccommodationController> localizer,
            IWebHostEnvironment env)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _localizer = localizer;
            _env = env;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        [HttpGet("/Accommodation")]
        [HttpGet("/Accommodation/Index")]
        [HttpGet("/{slug}/Accommodation")]
        [HttpGet("/{slug}/Accommodation/Index")]
        public async Task<IActionResult> Index(string? slug = null, Guid? conferenceId = null)
        {
            var selectedConferenceId = conferenceId;

            if (!selectedConferenceId.HasValue || selectedConferenceId.Value == Guid.Empty)
            {
                selectedConferenceId = _selectedConferenceService.GetSelectedConferenceId();
            }

            var conferenceQuery = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(slug))
            {
                if (_tenantContext.Current == null ||
                    !string.Equals(_tenantContext.Current.Slug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = T(
                        "Error_InvalidConference",
                        "Geçerli bir kongre seçiniz.");

                    return Redirect("/Dashboard/MyConferences");
                }

                conferenceQuery = conferenceQuery
                    .Where(c => c.TenantId == _tenantContext.Current.Id);
            }

            Conference? conference = null;

            if (selectedConferenceId.HasValue && selectedConferenceId.Value != Guid.Empty)
            {
                conference = await conferenceQuery
                    .FirstOrDefaultAsync(c => c.Id == selectedConferenceId.Value);
            }
            else if (_tenantContext.Current != null)
            {
                conference = await conferenceQuery
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefaultAsync();
            }

            if (conference == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_ConferenceNotFound",
                    "Kongre bulunamadı.");

                return Redirect("/Dashboard/MyConferences");
            }

            _selectedConferenceService.SetSelectedConferenceId(conference.Id);

            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", conference.Tenant?.Slug ?? slug ?? "");
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            var hotels = await _context.Hotels
                .AsNoTracking()
                .Include(h => h.RoomTypes)
                .Include(h => h.Conference)
                .Where(h => h.ConferenceId == conference.Id)
                .OrderBy(h => h.Name)
                .ToListAsync();

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Slug = conference.Tenant?.Slug ?? slug;

            return View(hotels);
        }

        [HttpGet("/{slug}/Accommodation/Book/{roomTypeId:guid}")]
        [HttpGet("/Accommodation/Book/{roomTypeId:guid}")]
        public async Task<IActionResult> Book(string? slug, Guid roomTypeId, Guid? conferenceId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var roomType = await _context.RoomTypes
                .AsNoTracking()
                .Include(r => r.Hotel)
                    .ThenInclude(h => h.Conference)
                        .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(r => r.Id == roomTypeId);

            if (roomType == null) return NotFound();

            var conference = roomType.Hotel.Conference;
            var resolvedSlug = conference.Tenant?.Slug ?? slug ?? "";

            var bookedCount = await _context.AccommodationBookings
                .CountAsync(b => b.RoomTypeId == roomTypeId);
            if (bookedCount >= roomType.TotalQuota)
            {
                TempData["ErrorMessage"] = "Bu oda tipi için kontenjan dolmuştur.";
                return Redirect($"/{resolvedSlug}/Accommodation?conferenceId={conference.Id}");
            }

            var existing = await _context.AccommodationBookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.ConferenceId == conference.Id && b.AppUserId == user.Id);
            if (existing != null)
            {
                TempData["InfoMessage"] = "Bu kongre için zaten bir konaklama rezervasyonunuz bulunmaktadır.";
                return Redirect($"/{resolvedSlug}/Accommodation/MyBooking?conferenceId={conference.Id}");
            }

            var transfers = await _context.TransferOptions
                .AsNoTracking()
                .Where(t => t.ConferenceId == conference.Id)
                .OrderBy(t => t.Name)
                .ToListAsync();

            ViewBag.Slug = resolvedSlug;
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceTitle = conference.Title;
            ViewBag.Transfers = transfers;
            ViewBag.ConferenceStart = conference.StartDate;
            ViewBag.ConferenceEnd = conference.EndDate;

            return View(roomType);
        }

        [HttpPost("/{slug}/Accommodation/Book/{roomTypeId:guid}")]
        [HttpPost("/Accommodation/Book/{roomTypeId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookPost(string? slug, Guid roomTypeId,
            DateTime checkInDate, DateTime checkOutDate,
            string? roommateName, Guid? transferOptionId, Guid conferenceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var roomType = await _context.RoomTypes
                .Include(r => r.Hotel)
                    .ThenInclude(h => h.Conference)
                        .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(r => r.Id == roomTypeId);

            if (roomType == null) return NotFound();

            var conference = roomType.Hotel.Conference;
            var resolvedSlug = conference.Tenant?.Slug ?? slug ?? "";

            var bookedCount = await _context.AccommodationBookings
                .CountAsync(b => b.RoomTypeId == roomTypeId);
            if (bookedCount >= roomType.TotalQuota)
            {
                TempData["ErrorMessage"] = "Bu oda tipi için kontenjan dolmuştur.";
                return Redirect($"/{resolvedSlug}/Accommodation?conferenceId={conference.Id}");
            }

            if (checkInDate >= checkOutDate || checkInDate.Date < DateTime.UtcNow.AddHours(-12).Date)
            {
                TempData["ErrorMessage"] = "Geçersiz tarih aralığı.";
                return Redirect($"/{resolvedSlug}/Accommodation/Book/{roomTypeId}?conferenceId={conference.Id}");
            }

            var nights = (checkOutDate.Date - checkInDate.Date).Days;
            var roomTotal = roomType.Price * nights;

            TransferOption? transfer = null;
            decimal transferTotal = 0;
            if (transferOptionId.HasValue && transferOptionId.Value != Guid.Empty)
            {
                transfer = await _context.TransferOptions.FindAsync(transferOptionId.Value);
                transferTotal = transfer?.Price ?? 0;
            }

            var booking = new AccommodationBooking
            {
                Id = Guid.NewGuid(),
                AppUserId = user.Id,
                ConferenceId = conference.Id,
                RoomTypeId = roomTypeId,
                TransferOptionId = transfer?.Id,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate,
                RoommateName = roommateName?.Trim(),
                TotalAmount = roomTotal + transferTotal,
                IsPaid = false,
                CreatedDate = DateTime.UtcNow
            };

            _context.AccommodationBookings.Add(booking);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Rezervasyonunuz alındı. Ödeme tamamlandıktan sonra onaylanacaktır.";
            return Redirect($"/{resolvedSlug}/Accommodation/MyBooking?conferenceId={conference.Id}");
        }

        [HttpGet("/{slug}/Accommodation/MyBooking")]
        [HttpGet("/Accommodation/MyBooking")]
        public async Task<IActionResult> MyBooking(string? slug, Guid? conferenceId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var selectedId = conferenceId ?? _selectedConferenceService.GetSelectedConferenceId();
            if (!selectedId.HasValue)
            {
                TempData["ErrorMessage"] = "Kongre seçilmedi.";
                return Redirect("/Dashboard/MyConferences");
            }

            var booking = await _context.AccommodationBookings
                .AsNoTracking()
                .Include(b => b.RoomType)
                    .ThenInclude(r => r.Hotel)
                .Include(b => b.TransferOption)
                .Include(b => b.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(b => b.ConferenceId == selectedId.Value && b.AppUserId == user.Id);

            if (booking == null)
            {
                TempData["InfoMessage"] = "Bu kongre için konaklama rezervasyonunuz bulunmamaktadır.";
                return Redirect($"/{slug}/Accommodation?conferenceId={selectedId}");
            }

            ViewBag.Slug = booking.Conference.Tenant?.Slug ?? slug ?? "";
            ViewBag.ConferenceTitle = booking.Conference.Title;

            return View(booking);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("/Admin/Accommodation/SeedData")]
        public async Task<IActionResult> SeedData(Guid? conferenceId = null)
        {
            // Bu action yalnızca Development ortamında çalışır
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            Conference? conference = null;

            if (conferenceId.HasValue && conferenceId.Value != Guid.Empty)
            {
                conference = await _context.Conferences
                    .FirstOrDefaultAsync(c => c.Id == conferenceId.Value);
            }
            else
            {
                conference = await _context.Conferences
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefaultAsync();
            }

            if (conference == null)
            {
                return Content(T("NoConferenceFound", "Kongre bulunamadı."));
            }

            var alreadyExists = await _context.Hotels
                .AnyAsync(h => h.ConferenceId == conference.Id);

            if (alreadyExists)
            {
                return Content(T("SeedAlreadyExists", "Bu kongre için otel verisi zaten mevcut."));
            }

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
                new RoomType
                {
                    Name = "Tek Kişilik Oda",
                    Price = 1500,
                    Currency = "TL",
                    Capacity = 1,
                    TotalQuota = 50,
                    HotelId = newHotel.Id
                },
                new RoomType
                {
                    Name = "Çift Kişilik Oda (Double)",
                    Price = 2500,
                    Currency = "TL",
                    Capacity = 2,
                    TotalQuota = 30,
                    HotelId = newHotel.Id
                },
                new RoomType
                {
                    Name = "Deluxe Suite",
                    Price = 5000,
                    Currency = "TL",
                    Capacity = 3,
                    TotalQuota = 5,
                    HotelId = newHotel.Id
                }
            };

            _context.RoomTypes.AddRange(rooms);
            await _context.SaveChangesAsync();

            return Content(T("SeedSuccess", "Konaklama test verileri başarıyla oluşturuldu."));
        }
    }
}