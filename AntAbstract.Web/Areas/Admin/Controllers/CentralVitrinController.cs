using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.WebsiteBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    [Route("Admin/CentralVitrin/{action=Index}/{id?}")]
    public class CentralVitrinController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IStringLocalizer<CentralVitrinController> _localizer;

        public CentralVitrinController(
            AppDbContext context,
            IStringLocalizer<CentralVitrinController> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            Guid? tenantId,
            string? status)
        {
            var now = DateTime.Now;

            var query = _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .AsQueryable();

            ViewBag.TotalConferenceCount = await _context.Conferences
                .AsNoTracking()
                .CountAsync();

            ViewBag.ActiveConferenceCount = await _context.Conferences
                .AsNoTracking()
                .CountAsync(c => c.StartDate <= now && c.EndDate >= now);

            ViewBag.UpcomingConferenceCount = await _context.Conferences
                .AsNoTracking()
                .CountAsync(c => c.StartDate > now);

            ViewBag.CompletedConferenceCount = await _context.Conferences
                .AsNoTracking()
                .CountAsync(c => c.EndDate < now);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();

                query = query.Where(c =>
                    (c.Title != null && c.Title.Contains(keyword)) ||
                    (c.Slug != null && c.Slug.Contains(keyword)) ||
                    (c.City != null && c.City.Contains(keyword)) ||
                    (c.Country != null && c.Country.Contains(keyword)) ||
                    (c.Tenant != null && c.Tenant.Name != null && c.Tenant.Name.Contains(keyword)) ||
                    (c.Tenant != null && c.Tenant.Slug != null && c.Tenant.Slug.Contains(keyword)));
            }

            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                query = query.Where(c => c.TenantId == tenantId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedStatus = status.Trim();

                if (normalizedStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.StartDate <= now && c.EndDate >= now);
                }
                else if (normalizedStatus.Equals("Upcoming", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.StartDate > now);
                }
                else if (normalizedStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.EndDate < now);
                }
            }

            var conferences = await query
                .OrderByDescending(c => c.StartDate)
                .ThenBy(c => c.Title)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.SelectedTenantId = tenantId;
            ViewBag.SelectedStatus = string.IsNullOrWhiteSpace(status)
                ? "All"
                : status;

            ViewBag.Tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name,
                    Selected = tenantId.HasValue && tenantId.Value == t.Id
                })
                .ToListAsync();

            return View(conferences);
        }

        [HttpGet]
        public async Task<IActionResult> ManageBlocks(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var conference = await _context.Conferences
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            ViewBag.ConferenceName = conference.Title;
            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceSlug = conference.Tenant?.Slug ?? "";

            var blocks = await _context.ConferencePageBlocks
                .Where(b => b.ConferenceId == conferenceId)
                .OrderBy(b => b.Order)
                .ToListAsync();

            var academicTemplate = new List<(ConferencePageBlockType Type, string Title, int Order)>
            {
                (ConferencePageBlockType.Hero, "Ana Karşılama (Hero)", 1),
                (ConferencePageBlockType.CallForPapers, "Kongreye Çağrı", 2),
                (ConferencePageBlockType.Topics, "Kongre Konuları", 3),
                (ConferencePageBlockType.Committees, "Bilim Kurulu", 4),
                (ConferencePageBlockType.About, "Hakem Değerlendirme Süreci", 5),
                (ConferencePageBlockType.Fees, "Katılım Ücreti", 6),
                (ConferencePageBlockType.About, "Kongre Programı", 7)
            };

            var isNewBlockAdded = false;

            foreach (var item in academicTemplate)
            {
                var exists = blocks.Any(b =>
                    b.Title == item.Title &&
                    b.ConferenceId == conferenceId);

                if (!exists)
                {
                    var newBlock = new ConferencePageBlock
                    {
                        ConferenceId = conferenceId,
                        TenantId = conference.TenantId,
                        BlockType = item.Type,
                        Title = item.Title,
                        IsActive = true,
                        Order = item.Order,
                        ContentJson = "{}",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.ConferencePageBlocks.Add(newBlock);
                    blocks.Add(newBlock);

                    isNewBlockAdded = true;
                }
            }

            if (isNewBlockAdded)
            {
                await _context.SaveChangesAsync();

                blocks = blocks
                    .OrderBy(b => b.Order)
                    .ToList();
            }

            return View(blocks);
        }

        /// <summary>
        /// Henüz sitesi olmayan bir kongre seçtirir.
        ///
        /// Admin/Website/InitSite bu iş için kullanılamıyor: o controller
        /// TenantAdminOnly politikasına bağlı ve SuperAdmin'i dışlıyor, ayrıca
        /// kurum bağlamı (slug) gerektiriyor. Buradaki ekran global çalışır.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> InitSite()
        {
            var withBlocks = await _context.ConferencePageBlocks
                .AsNoTracking()
                .Select(b => b.ConferenceId)
                .Distinct()
                .ToListAsync();

            var available = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .Where(c => !withBlocks.Contains(c.Id))
                .OrderByDescending(c => c.StartDate)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Tenant != null
                        ? c.Title + " — " + c.Tenant.Name
                        : c.Title
                })
                .ToListAsync();

            ViewBag.ConferenceList = available;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitSite(Guid conferenceId)
        {
            var conference = await _context.Conferences
                .FirstOrDefaultAsync(c => c.Id == conferenceId);

            if (conference == null)
            {
                TempData["ErrorMessage"] = _localizer["ConferenceNotFound"].Value;
                return RedirectToAction(nameof(InitSite));
            }

            // Bloklar ManageBlocks açılırken oluşturuluyor; şablonun tek yerde
            // kalması için burada kopyalamak yerine oraya yönlendiriyoruz.
            TempData["SuccessMessage"] = _localizer["SiteCreated"].Value;

            return RedirectToAction(
                nameof(ManageBlocks),
                new { conferenceId });
        }

        [HttpGet]
        public async Task<IActionResult> EditBlock(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var block = await _context.ConferencePageBlocks
                .Include(b => b.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (block == null)
            {
                return NotFound();
            }

            ViewBag.BlockType = block.BlockType;
            ViewBag.ConferenceId = block.ConferenceId;
            ViewBag.ConferenceName = block.Conference?.Title ?? "";
            ViewBag.ConferenceSlug = block.Conference?.Tenant?.Slug ?? "";

            FillBlockContentViewBag(block);

            return View(block);
        }

        /// <summary>
        /// Bloğun ContentJson'ını tipine göre çözerek ilgili ViewBag alanına koyar.
        /// Bozuk JSON düzenlemeyi engellememeli, o yüzden boş içerikle devam edilir.
        /// </summary>
        private void FillBlockContentViewBag(ConferencePageBlock block)
        {
            switch (block.BlockType)
            {
                case ConferencePageBlockType.About:
                    ViewBag.AboutContent = Parse<AboutBlockContent>(block.ContentJson);
                    break;

                // Clean(...) hem boş satırları eler hem de JSON'da "null" gelen
                // koleksiyonları boş listeye çevirir; editör null listede patlamamalı.

                case ConferencePageBlockType.Topics:
                    ViewBag.TopicsContent = Clean(Parse<TopicsBlockContent>(block.ContentJson));
                    break;

                case ConferencePageBlockType.ImportantDates:
                    ViewBag.DatesContent = Clean(Parse<ImportantDatesBlockContent>(block.ContentJson));
                    break;

                case ConferencePageBlockType.Fees:
                    ViewBag.FeesContent = Clean(Parse<FeesBlockContent>(block.ContentJson));
                    break;

                case ConferencePageBlockType.Committees:
                    ViewBag.CommitteesContent = Clean(Parse<CommitteesBlockContent>(block.ContentJson));
                    break;

                case ConferencePageBlockType.Contact:
                    ViewBag.ContactContent = Parse<ContactBlockContent>(block.ContentJson);
                    break;

                case ConferencePageBlockType.FAQ:
                    ViewBag.FaqContent = Clean(Parse<FaqBlockContent>(block.ContentJson));
                    break;

                case ConferencePageBlockType.Sponsors:
                    ViewBag.SponsorContent = Clean(Parse<SponsorBlockContent>(block.ContentJson));
                    break;

                case ConferencePageBlockType.CallForPapers:
                    ViewBag.CallContent = Clean(Parse<CallForPapersBlockContent>(block.ContentJson));
                    break;
            }
        }

        private static T Parse<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new T();
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        // ── Boş satır temizliği ───────────────────────────────────────────────
        // Formdaki tekrarlayıcı, kullanıcı doldurmadan satır ekleyebiliyor.
        // Kaydetmeden önce anlamsız satırları atıyoruz ki siteye boş kutu çıkmasın.

        private static TopicsBlockContent Clean(TopicsBlockContent c)
        {
            c.Items = (c.Items ?? new())
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            return c;
        }

        private static ImportantDatesBlockContent Clean(ImportantDatesBlockContent c)
        {
            c.Items = (c.Items ?? new())
                .Where(x => !string.IsNullOrWhiteSpace(x.Label) ||
                            !string.IsNullOrWhiteSpace(x.Date))
                .ToList();

            return c;
        }

        private static FeesBlockContent Clean(FeesBlockContent c)
        {
            c.Items = (c.Items ?? new())
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            return c;
        }

        private static CommitteesBlockContent Clean(CommitteesBlockContent c)
        {
            c.Groups = (c.Groups ?? new())
                .Select(g =>
                {
                    g.Members = (g.Members ?? new())
                        .Where(m => !string.IsNullOrWhiteSpace(m.FullName))
                        .ToList();

                    return g;
                })
                .Where(g => !string.IsNullOrWhiteSpace(g.Name) || g.Members.Count > 0)
                .ToList();

            return c;
        }

        private static FaqBlockContent Clean(FaqBlockContent c)
        {
            c.Questions = (c.Questions ?? new())
                .Where(x => !string.IsNullOrWhiteSpace(x.Question))
                .ToList();

            return c;
        }

        private static SponsorBlockContent Clean(SponsorBlockContent c)
        {
            c.Sponsors = (c.Sponsors ?? new())
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            return c;
        }

        private static CallForPapersBlockContent Clean(CallForPapersBlockContent c)
        {
            c.Guidelines = (c.Guidelines ?? new())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return c;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBlock(
            int id,
            ConferencePageBlock model,
            AboutBlockContent aboutContent,
            TopicsBlockContent topicsContent,
            ImportantDatesBlockContent datesContent,
            FeesBlockContent feesContent,
            CommitteesBlockContent committeesContent,
            ContactBlockContent contactContent,
            FaqBlockContent faqContent,
            SponsorBlockContent sponsorContent,
            CallForPapersBlockContent callContent,
            string? repeaterReady = null)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var block = await _context.ConferencePageBlocks
                .FirstOrDefaultAsync(x => x.Id == id);

            if (block == null)
            {
                return NotFound();
            }

            block.Title = model.Title;
            block.Subtitle = model.Subtitle;
            block.IsActive = model.IsActive;
            block.UpdatedAt = DateTime.UtcNow;

            // Satır listelerinin alan adlarını tarayıcıdaki betik üretiyor. Betik
            // çalışmadıysa liste boş görünür; bunu "hepsini sil" sanıp mevcut
            // içeriği ezmemek için o tipleri olduğu gibi bırakıyoruz.
            var listsSubmitted = repeaterReady == "1";

            var usesLists = block.BlockType
                is ConferencePageBlockType.Topics
                or ConferencePageBlockType.ImportantDates
                or ConferencePageBlockType.Fees
                or ConferencePageBlockType.Committees
                or ConferencePageBlockType.FAQ
                or ConferencePageBlockType.Sponsors
                or ConferencePageBlockType.CallForPapers;

            if (usesLists && !listsSubmitted)
            {
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = _localizer["BlockContentNotSaved"].Value;

                return RedirectToAction(
                    nameof(ManageBlocks),
                    new { conferenceId = block.ConferenceId });
            }

            // Blok tipi sistemce belirlenir; formdan gelen değere güvenilmez.
            block.ContentJson = block.BlockType switch
            {
                ConferencePageBlockType.About =>
                    JsonSerializer.Serialize(aboutContent),

                ConferencePageBlockType.Topics =>
                    JsonSerializer.Serialize(Clean(topicsContent)),

                ConferencePageBlockType.ImportantDates =>
                    JsonSerializer.Serialize(Clean(datesContent)),

                ConferencePageBlockType.Fees =>
                    JsonSerializer.Serialize(Clean(feesContent)),

                ConferencePageBlockType.Committees =>
                    JsonSerializer.Serialize(Clean(committeesContent)),

                ConferencePageBlockType.Contact =>
                    JsonSerializer.Serialize(contactContent),

                ConferencePageBlockType.FAQ =>
                    JsonSerializer.Serialize(Clean(faqContent)),

                ConferencePageBlockType.Sponsors =>
                    JsonSerializer.Serialize(Clean(sponsorContent)),

                ConferencePageBlockType.CallForPapers =>
                    JsonSerializer.Serialize(Clean(callContent)),

                _ => block.ContentJson
            };

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["BlockUpdatedSuccessfully"].Value;

            return RedirectToAction(
                nameof(ManageBlocks),
                new { conferenceId = block.ConferenceId });
        }

        [HttpGet]
        public async Task<IActionResult> CreateBlock(Guid conferenceId)
        {
            if (conferenceId == Guid.Empty)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var conference = await _context.Conferences
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == conferenceId);

            if (conference == null)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            ViewBag.ConferenceId = conference.Id;
            ViewBag.ConferenceName = conference.Title ?? "";
            ViewBag.ConferenceSlug = conference.Tenant?.Slug ?? "";

            return View(new ConferencePageBlock
            {
                ConferenceId = conference.Id,
                TenantId = conference.TenantId,
                IsActive = true,
                BlockType = ConferencePageBlockType.About,
                ContentJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlock(ConferencePageBlock model)
        {
            if (model.ConferenceId == Guid.Empty)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var conference = await _context.Conferences
                .FirstOrDefaultAsync(x => x.Id == model.ConferenceId);

            if (conference == null)
            {
                return NotFound(_localizer["ConferenceNotFound"]);
            }

            var lastOrder = await _context.ConferencePageBlocks
                .Where(b => b.ConferenceId == model.ConferenceId)
                .MaxAsync(b => (int?)b.Order) ?? 0;

            model.TenantId = conference.TenantId;
            model.Order = lastOrder + 1;
            model.CreatedAt = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(model.ContentJson))
            {
                model.ContentJson = "{}";
            }

            _context.ConferencePageBlocks.Add(model);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["BlockCreatedSuccessfully"].Value;

            return RedirectToAction(
                nameof(ManageBlocks),
                new { conferenceId = model.ConferenceId });
        }
    }
}