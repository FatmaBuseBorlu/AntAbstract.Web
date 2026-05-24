using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Infrastructure.Services.Conferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public AccommodationController(
            AppDbContext context,
            TenantContext tenantContext,
            ISelectedConferenceService selectedConferenceService,
            UserManager<AppUser> userManager,
            IStringLocalizer<AccommodationController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _selectedConferenceService = selectedConferenceService;
            _userManager = userManager;
            _localizer = localizer;
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

        [Authorize(Roles = "Admin")]
        [HttpGet("/Admin/Accommodation/SeedData")]
        public async Task<IActionResult> SeedData(Guid? conferenceId = null)
        {
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