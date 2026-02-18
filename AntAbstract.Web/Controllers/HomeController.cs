using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Models.ViewModels.Website;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;

namespace AntAbstract.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConferencePageBlockService _pageBlockService;

        public HomeController(
            AppDbContext context,
            TenantContext tenantContext,
            UserManager<AppUser> userManager,
            IConferencePageBlockService pageBlockService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
            _pageBlockService = pageBlockService;
        }

        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current != null)
            {
                var currentConference = await _context.Conferences
                    .Include(c => c.Tenant)
                    .Include(c => c.Registrations)
                    .Where(c => c.TenantId == _tenantContext.Current.Id)
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefaultAsync();

                if (currentConference == null)
                    return NotFound("Kongre aktif deðil.");

                var culture = HttpContext.Features.Get<IRequestCultureFeature>()?
                                  .RequestCulture.UICulture.Name
                              ?? CultureInfo.CurrentUICulture.Name
                              ?? "tr-TR";

                var page = "Home";

                var blocks = await _pageBlockService.GetBlocksAsync(
                    tenantId: _tenantContext.Current.Id,
                    conferenceId: currentConference.Id,
                    page: page,
                    culture: culture
                );

                var vm = new ConferenceHomePageViewModel
                {
                    Conference = currentConference,
                    Blocks = blocks,
                    Culture = culture,
                    Page = page
                };

                return View("ConferenceHome", vm);
            }

            var user = await _userManager.GetUserAsync(User);
            var registeredConferenceIds = new List<Guid>();

            if (user != null)
            {
                registeredConferenceIds = await _context.Registrations
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId)
                    .ToListAsync();
            }

            var conferences = await _context.Conferences
                .Where(c => c.EndDate > DateTime.Now)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            var lastSubmissions = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.Conference)
                .Include(s => s.Author)
                .Where(s => s.Status == SubmissionStatus.Accepted)
                .OrderByDescending(s => s.CreatedDate)
                .Take(4)
                .Select(s => new SubmissionCardDto
                {
                    Title = s.Title,

                    AbstractSnippet = (s.Abstract != null && s.Abstract.Length > 120)
                        ? s.Abstract.Substring(0, 120) + "..."
                        : s.Abstract ?? "Özet metni bulunmuyor.",

                    AuthorName = s.Author != null
                        ? $"{s.Author.FirstName} {s.Author.LastName}"
                        : "Misafir Kullanýcý",

                    University = s.Author != null
                        ? (s.Author.Institution ?? "Kurum Belirtilmemiþ")
                        : "",

                    ConferenceName = s.Conference.Title,

                    AuthorImageUrl = (s.Author != null && !string.IsNullOrEmpty(s.Author.ProfileImagePath))
                        ? s.Author.ProfileImagePath
                        : $"https://ui-avatars.com/api/?name={(s.Author != null ? s.Author.FirstName : "A")}+{(s.Author != null ? s.Author.LastName : "A")}&background=random&color=fff"
                })
                .ToListAsync();

            var model = new LandingPageViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                ActiveCongressesCount = conferences.Count,

                ActiveCongresses = conferences.Select(c => new CongressCardDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description ?? "Detaylar için inceleyiniz.",
                    StartDate = c.StartDate,
                    Location = string.IsNullOrEmpty(c.City) ? "Online" : $"{c.City} {(c.Country != null ? "/ " + c.Country : "")}",
                    ImageUrl = string.IsNullOrEmpty(c.BannerPath) ? "/abstract/upload/img/resimyok3.png" : c.BannerPath,
                    Slug = c.Slug ?? c.Id.ToString(),
                    IsRegistered = registeredConferenceIds.Contains(c.Id)
                }).ToList(),

                LastSubmissions = lastSubmissions
            };

            return View(model);
        }

        public async Task<IActionResult> Congresses()
        {
            var allCongresses = await _context.Conferences
                .Include(c => c.Tenant)
                .Include(c => c.Registrations)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            return View(allCongresses);
        }

        public IActionResult About() => View();
        public IActionResult Contact() => View();
        public IActionResult Privacy() => View();

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
