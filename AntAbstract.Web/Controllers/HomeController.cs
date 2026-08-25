using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using AntAbstract.Web.Models.ViewModels.Shared;
using AntAbstract.Web.Models.ViewModels.Website;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
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
        private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider;

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
            IStringLocalizer<HomeController> localizer,
            IActionDescriptorCollectionProvider actionDescriptorProvider)
        {
            _context = context;
            _tenantContext = tenantContext;
            _userManager = userManager;
            _pageBlockService = pageBlockService;
            _localizer = localizer;
            _actionDescriptorProvider = actionDescriptorProvider;
        }

        /// <summary>
        /// URL'deki ilk segment gerçek bir controller veya area adına karşılık geliyor mu?
        /// "/Payment" gibi adresler de {slug}/... route'una takıldığı için, bunları
        /// "olmayan kongre" sayıp 404 döndürmemek gerekir.
        /// </summary>
        private bool IsReservedRouteSegment(string segment)
        {
            foreach (var descriptor in _actionDescriptorProvider.ActionDescriptors.Items)
            {
                if (descriptor is not ControllerActionDescriptor controllerDescriptor)
                {
                    continue;
                }

                if (string.Equals(
                        controllerDescriptor.ControllerName,
                        segment,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (controllerDescriptor.RouteValues.TryGetValue("area", out var area) &&
                    !string.IsNullOrEmpty(area) &&
                    string.Equals(area, segment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// "Böyle bir kongre yok" sayfasını 404 durum koduyla döndürür.
        /// </summary>
        private IActionResult ConferenceNotFound(string? slug, string message)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;

            ViewBag.Slug = slug;
            ViewBag.NotFoundMessage = message;

            return View("ConferenceNotFound");
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

        public async Task<IActionResult> Index(Guid? conferenceId = null)
        {
            if (_tenantContext.Current != null)
            {
                // Kongre bazlı slug: middleware'de resolve edilen kongreyi kullan
                Conference? currentConference = _tenantContext.CurrentConference;

                if (currentConference == null)
                {
                    var conferencesQuery = _context.Conferences
                        .AsNoTracking()
                        .Include(c => c.Tenant)
                        .Include(c => c.Registrations)
                        .Where(c => c.TenantId == _tenantContext.Current.Id);

                    if (conferenceId.HasValue)
                    {
                        currentConference = await conferencesQuery
                            .FirstOrDefaultAsync(c => c.Id == conferenceId.Value);
                    }

                    currentConference ??= await conferencesQuery
                        .OrderByDescending(c => c.StartDate)
                        .FirstOrDefaultAsync();
                }

                if (currentConference == null)
                {
                    return ConferenceNotFound(
                        _tenantContext.Current.Slug,
                        T("ConferenceNotActive", "Aktif kongre bulunamadı."));
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

            // URL'de bir slug segmenti var ama hiçbir kongreye/kuruma karşılık gelmiyor.
            // Bu durumda ana sayfayı 200 ile göstermek yerine 404 dönülür; aksi hâlde
            // yazım hatası yapan kullanıcı doğru sayfada olduğunu sanır ve kırık
            // bağlantılar sunucu loglarında hiç görünmez.
            var requestedSlug = RouteData.Values["slug"]?.ToString();

            if (!string.IsNullOrWhiteSpace(requestedSlug) &&
                !IsReservedRouteSegment(requestedSlug))
            {
                return ConferenceNotFound(
                    requestedSlug,
                    T("ConferenceSlugNotFound", "Böyle bir kongre bulunamadı."));
            }

            var user = await _userManager.GetUserAsync(User);

            var registeredIds = new List<Guid>();

            if (user != null)
            {
                registeredIds = await _context.Registrations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.AppUserId == user.Id)
                    .Select(r => r.ConferenceId)
                    .ToListAsync();
            }

            var allConferences = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            var conferences = allConferences
                .Where(c => c.EndDate >= DateTime.Now)
                .OrderBy(c => c.StartDate)
                .ToList();

            var pastConferences = allConferences
                .Where(c => c.EndDate < DateTime.Now)
                .OrderByDescending(c => c.EndDate)
                .ToList();

            var proceedingBooks = allConferences
                .Where(c => c.IsProceedingBookPublished)
                .OrderByDescending(c => c.ProceedingBookPublishedDate ?? c.EndDate)
                .ToList();

            var abstractNotFoundText = T("AbstractNotFound", "Özet bulunamadı.");
            var guestUserText = T("GuestUser", "Misafir kullanıcı");
            var institutionNotSpecifiedText = T("InstitutionNotSpecified", "Kurum belirtilmedi");
            var reviewDetailsText = T("ReviewForDetails", "Detaylar için inceleyiniz.");
            var onlineText = T("Online", "Online");
            var isEn = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

            var lastSubmissions = await _context.Submissions
                .IgnoreQueryFilters()
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
                        : institutionNotSpecifiedText,

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

                    Title = isEn && !string.IsNullOrWhiteSpace(c.TitleEn) ? c.TitleEn : c.Title,

                    Description = Shorten(
                        ToPlainText(isEn && !string.IsNullOrWhiteSpace(c.DescriptionEn)
                            ? c.DescriptionEn
                            : string.IsNullOrWhiteSpace(c.Description)
                                ? reviewDetailsText
                                : c.Description),
                        90
                    ),

                    StartDate = c.StartDate,

                    EndDate = c.EndDate,

                    Location = string.IsNullOrWhiteSpace(c.City)
                        ? onlineText
                        : isEn
                            ? $"{(string.IsNullOrWhiteSpace(c.CityEn) ? c.City : c.CityEn)}{(string.IsNullOrWhiteSpace(c.CountryEn) ? (string.IsNullOrWhiteSpace(c.Country) ? "" : " / " + c.Country) : " / " + c.CountryEn)}"
                            : $"{c.City}{(string.IsNullOrWhiteSpace(c.Country) ? "" : " / " + c.Country)}",

                    ImageUrl = string.IsNullOrWhiteSpace(c.BannerPath)
                        ? "/abstract/upload/img/resimyok3.png"
                        : c.BannerPath,

                    Slug = c.Tenant != null && !string.IsNullOrWhiteSpace(c.Tenant.Slug)
                        ? c.Tenant.Slug
                        : !string.IsNullOrWhiteSpace(c.Slug)
                            ? c.Slug
                            : c.Id.ToString(),

                    IsRegistered = registeredIds.Contains(c.Id),

                    IsSubmissionOpen = c.IsSubmissionAvailable,

                    IsRegistrationOpen = c.IsRegistrationAvailable,

                    AbstractSubmissionDeadline = c.AbstractSubmissionDeadline
                }).ToList(),

                PastCongresses = pastConferences.Select(c => new CongressCardDto
                {
                    Id = c.Id,
                    Title = isEn && !string.IsNullOrWhiteSpace(c.TitleEn) ? c.TitleEn : c.Title,
                    Description = Shorten(ToPlainText(isEn && !string.IsNullOrWhiteSpace(c.DescriptionEn) ? c.DescriptionEn : string.IsNullOrWhiteSpace(c.Description) ? reviewDetailsText : c.Description), 90),
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Location = string.IsNullOrWhiteSpace(c.City) ? onlineText : isEn ? $"{(string.IsNullOrWhiteSpace(c.CityEn) ? c.City : c.CityEn)}{(string.IsNullOrWhiteSpace(c.CountryEn) ? (string.IsNullOrWhiteSpace(c.Country) ? "" : " / " + c.Country) : " / " + c.CountryEn)}" : $"{c.City}{(string.IsNullOrWhiteSpace(c.Country) ? "" : " / " + c.Country)}",
                    ImageUrl = string.IsNullOrWhiteSpace(c.BannerPath) ? "/abstract/upload/img/resimyok3.png" : c.BannerPath,
                    Slug = c.Tenant != null && !string.IsNullOrWhiteSpace(c.Tenant.Slug) ? c.Tenant.Slug : !string.IsNullOrWhiteSpace(c.Slug) ? c.Slug : c.Id.ToString(),
                    IsRegistered = registeredIds.Contains(c.Id),
                    IsSubmissionOpen = false,
                    IsRegistrationOpen = false,
                    AbstractSubmissionDeadline = null
                }).ToList(),

                ProceedingBooks = proceedingBooks.Select(c => new ProceedingBookCardDto
                {
                    ConferenceId = c.Id,
                    ConferenceTitle = isEn && !string.IsNullOrWhiteSpace(c.TitleEn) ? c.TitleEn : c.Title,
                    Slug = c.Tenant != null && !string.IsNullOrWhiteSpace(c.Tenant.Slug) ? c.Tenant.Slug : !string.IsNullOrWhiteSpace(c.Slug) ? c.Slug : c.Id.ToString(),
                    PublishedDate = c.ProceedingBookPublishedDate,
                    FilePath = c.ProceedingBookFilePath,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Location = string.IsNullOrWhiteSpace(c.City) ? onlineText : isEn ? $"{(string.IsNullOrWhiteSpace(c.CityEn) ? c.City : c.CityEn)}{(string.IsNullOrWhiteSpace(c.CountryEn) ? (string.IsNullOrWhiteSpace(c.Country) ? "" : " / " + c.Country) : " / " + c.CountryEn)}" : $"{c.City}{(string.IsNullOrWhiteSpace(c.Country) ? "" : " / " + c.Country)}"
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
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(r => r.RegistrationType)
                    .Where(r => r.AppUserId == user.Id)
                    .ToListAsync();

                registrationsByConference = registrations
                    .GroupBy(r => r.ConferenceId)
                    .ToDictionary(g => g.Key, g => g.First());

                var submissions = await _context.Submissions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(s => s.AuthorId == user.Id)
                    .ToListAsync();

                submissionsByConference = submissions
                    .GroupBy(s => s.ConferenceId)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            var allCongresses = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Tenant)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            // Gerçekten kayıt alınabilen kongreler: kayıt açık, tarihi geçmemiş,
            // süresi dolmamış aktif bir kayıt türü var ve kontenjanı dolmamış.
            var today = DateTime.UtcNow.Date;

            var conferenceIdsWithOpenTypes = await _context.RegistrationTypes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(rt =>
                    rt.IsActive &&
                    (
                        !rt.Deadline.HasValue ||
                        rt.Deadline.Value.Date >= today
                    ))
                .Select(rt => rt.ConferenceId)
                .Distinct()
                .ToListAsync();

            var fullConferenceIds = await _context.Conferences
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c =>
                    c.MaxRegistrations.HasValue &&
                    c.Registrations.Count() >= c.MaxRegistrations.Value)
                .Select(c => c.Id)
                .ToListAsync();

            var registrationAvailableConferenceIds = allCongresses
                .Where(c =>
                    c.IsRegistrationOpen &&
                    c.EndDate.Date >= today &&
                    conferenceIdsWithOpenTypes.Contains(c.Id) &&
                    !fullConferenceIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToHashSet();

            ViewBag.IsSignedIn = user != null;
            ViewBag.RegistrationsByConference = registrationsByConference;
            ViewBag.SubmissionsByConference = submissionsByConference;
            ViewBag.RegistrationAvailableConferenceIds = registrationAvailableConferenceIds;

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
