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
using Microsoft.Extensions.Localization;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace AntAbstract.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConferencePageBlockService _pageBlockService;
        private readonly IStringLocalizer<HomeController> _localizer;

        private static readonly string[] SupportedCultures =
        {
            "tr-TR",
            "en-US"
        };

        public HomeController(
            AppDbContext context,
            TenantContext tenantContext,
            UserManager<AppUser> userManager,
            IConferencePageBlockService pageBlockService,
            IStringLocalizer<HomeController> localizer)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
            _pageBlockService = pageBlockService;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        private static string ToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var text = Regex.Replace(html, "<.*?>", " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

        private static string Shorten(string? text, int maxLength = 90)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength).Trim() + "...";
        }

        public async Task<IActionResult> Index()
        {
            if (_tenantContext.Current != null)
            {
                var currentConference = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Include(c => c.Registrations)
                    .Where(c => c.TenantId == _tenantContext.Current.Id)
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefaultAsync();

                if (currentConference == null)
                {
                    return NotFound(T(
                        "ConferenceNotActive",
                        "Aktif kongre bulunamadý."));
                }

                var currentUser = await _userManager.GetUserAsync(User);

                var registeredConferenceIds = new List<Guid>();

                if (currentUser != null)
                {
                    registeredConferenceIds = await _context.Registrations
                        .AsNoTracking()
                        .Where(r => r.AppUserId == currentUser.Id)
                        .Select(r => r.ConferenceId)
                        .ToListAsync();
                }

                ViewBag.RegisteredConferenceIds = registeredConferenceIds;

                var culture = HttpContext.Features.Get<IRequestCultureFeature>()?
                                  .RequestCulture.UICulture.Name
                              ?? CultureInfo.CurrentUICulture.Name
                              ?? "tr-TR";

                if (!SupportedCultures.Contains(culture))
                {
                    culture = "tr-TR";
                }

                var page = "Home";

                var blocks = await _pageBlockService.GetBlocksAsync(
                    tenantId: _tenantContext.Current.Id,
                    conferenceId: currentConference.Id,
                    page: page,
                    culture: culture
                );

                var suggestedConferences = await _context.Conferences
                    .AsNoTracking()
                    .Include(c => c.Tenant)
                    .Where(c =>
                        c.Id != currentConference.Id &&
                        c.EndDate > DateTime.Now)
                    .OrderBy(c => c.StartDate)
                    .Take(4)
                    .ToListAsync();

                var vm = new ConferenceHomePageViewModel
                {
                    Conference = currentConference,
                    Blocks = blocks,
                    Culture = culture,
                    Page = page,
                    SuggestedConferences = suggestedConferences
                };

                return View("ConferenceHome", vm);
            }

            var user = await _userManager.GetUserAsync(User);

            var registeredIds = new List<Guid>();

            if (user != null)
            {
                registeredIds = await _context.Registrations
                    .AsNoTracking()
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId)
                    .ToListAsync();
            }

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c => c.EndDate > DateTime.Now)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            var abstractNotFoundText = T("AbstractNotFound", "Özet bulunamadý.");
            var guestUserText = T("GuestUser", "Misafir kullanýcý");
            var institutionNotSpecifiedText = T("InstitutionNotSpecified", "Kurum belirtilmedi");
            var reviewDetailsText = T("ReviewForDetails", "Detaylar için inceleyiniz.");
            var onlineText = T("Online", "Online");

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

                    AbstractSnippet = s.Abstract != null && s.Abstract.Length > 120
                        ? s.Abstract.Substring(0, 120) + "..."
                        : s.Abstract ?? abstractNotFoundText,

                    AuthorName = s.Author != null
                        ? $"{s.Author.FirstName} {s.Author.LastName}"
                        : guestUserText,

                    University = s.Author != null
                        ? s.Author.Institution ?? institutionNotSpecifiedText
                        : "",

                    ConferenceName = s.Conference.Title,

                    AuthorImageUrl = s.Author != null && !string.IsNullOrEmpty(s.Author.ProfileImagePath)
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

                    Description = Shorten(
                        ToPlainText(string.IsNullOrWhiteSpace(c.Description)
                            ? reviewDetailsText
                            : c.Description),
                        90
                    ),

                    StartDate = c.StartDate,

                    Location = string.IsNullOrEmpty(c.City)
                        ? onlineText
                        : $"{c.City}{(c.Country != null ? " / " + c.Country : "")}",

                    ImageUrl = string.IsNullOrEmpty(c.BannerPath)
                        ? "/abstract/upload/img/resimyok3.png"
                        : c.BannerPath,

                    Slug = c.Tenant?.Slug ?? c.Slug ?? c.Id.ToString(),

                    IsRegistered = registeredIds.Contains(c.Id)
                }).ToList(),

                LastSubmissions = lastSubmissions
            };

            return View(model);
        }

        public async Task<IActionResult> Congresses()
        {
            var user = await _userManager.GetUserAsync(User);

            var registrationsByConference = new Dictionary<Guid, Registration>();
            var submissionsByConference = new Dictionary<Guid, List<Submission>>();

            if (user != null)
            {
                var registrations = await _context.Registrations
                    .AsNoTracking()
                    .Include(r => r.RegistrationType)
                    .Where(r => r.AppUserId == user.Id)
                    .ToListAsync();

                registrationsByConference = registrations
                    .GroupBy(r => r.ConferenceId)
                    .ToDictionary(g => g.Key, g => g.First());

                var submissions = await _context.Submissions
                    .AsNoTracking()
                    .Where(s => s.AuthorId == user.Id)
                    .ToListAsync();

                submissionsByConference = submissions
                    .GroupBy(s => s.ConferenceId)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            var allCongresses = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            ViewBag.IsSignedIn = user != null;
            ViewBag.RegistrationsByConference = registrationsByConference;
            ViewBag.SubmissionsByConference = submissionsByConference;

            return View(allCongresses);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet("/kvkk")]
        public IActionResult Kvkk()
        {
            return View();
        }

        [HttpGet("/cookies")]
        public IActionResult Cookies()
        {
            return View();
        }

        [HttpGet("/terms")]
        public IActionResult Terms()
        {
            return View();
        }

        [HttpGet("/proceedings")]
        public IActionResult Proceedings()
        {
            return Redirect("/Proceedings/Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            if (!SupportedCultures.Contains(culture))
            {
                culture = "tr-TR";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                }
            );

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
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