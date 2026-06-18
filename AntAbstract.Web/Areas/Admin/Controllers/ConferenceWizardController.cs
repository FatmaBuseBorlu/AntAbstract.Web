using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPolicies.TenantAdmin)]
    public class ConferenceWizardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenantContext;

        public ConferenceWizardController(AppDbContext context, TenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private static string GenerateSlug(string title)
        {
            var slug = title.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[çÇ]", "c");
            slug = Regex.Replace(slug, @"[ğĞ]", "g");
            slug = Regex.Replace(slug, @"[ışİŞ]", "s");
            slug = Regex.Replace(slug, @"[üÜ]", "u");
            slug = Regex.Replace(slug, @"[öÖ]", "o");
            slug = Regex.Replace(slug, @"[ıI]", "i");
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-").Trim('-');
            return slug.Length > 60 ? slug[..60] : slug;
        }

        // ── Adım 1: Temel Bilgiler ───────────────────────────────────────────

        [HttpGet("/{slug}/Admin/ConferenceWizard/Step1")]
        public IActionResult Step1(string slug)
        {
            ViewBag.Slug = slug;
            return View();
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step1")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step1Post(string slug,
            string title, string? description, string? city, string? country, string? venue)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                ModelState.AddModelError("title", "Kongre adı zorunludur.");
                ViewBag.Slug = slug;
                return View("Step1");
            }

            var tenant = _tenantContext.Current;
            if (tenant == null)
            {
                TempData["ErrorMessage"] = "Tenant bulunamadı.";
                return Redirect($"/{slug}/Admin/Conferences");
            }

            var confSlug = GenerateSlug(title);
            var slugExists = await _context.Conferences
                .AnyAsync(c => c.Slug == confSlug && c.TenantId == tenant.Id);
            if (slugExists)
            {
                confSlug = confSlug + "-" + DateTime.UtcNow.Year;
            }

            var conference = new Conference
            {
                Id = Guid.NewGuid(),
                Title = title.Trim(),
                Description = description?.Trim(),
                City = city?.Trim(),
                Country = country?.Trim(),
                Venue = venue?.Trim(),
                Slug = confSlug,
                TenantId = tenant.Id,
                StartDate = DateTime.Today.AddMonths(3),
                EndDate = DateTime.Today.AddMonths(3).AddDays(2),
                IsSubmissionOpen = false,
                IsRegistrationOpen = false,
            };

            _context.Conferences.Add(conference);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kongre oluşturuldu. Devam edin.";
            return Redirect($"/{slug}/Admin/ConferenceWizard/Step2/{conference.Id}");
        }

        // ── Adım 2: Tarihler & Deadline'lar ─────────────────────────────────

        [HttpGet("/{slug}/Admin/ConferenceWizard/Step2/{id:guid}")]
        public async Task<IActionResult> Step2(string slug, Guid id)
        {
            var conf = await _context.Conferences.FindAsync(id);
            if (conf == null) return NotFound();
            ViewBag.Slug = slug;
            return View(conf);
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step2/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step2Post(string slug, Guid id,
            DateTime startDate, DateTime endDate,
            DateTime? abstractDeadline, DateTime? fullTextDeadline)
        {
            var conf = await _context.Conferences.FindAsync(id);
            if (conf == null) return NotFound();

            if (startDate >= endDate)
            {
                TempData["ErrorMessage"] = "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.";
                return Redirect($"/{slug}/Admin/ConferenceWizard/Step2/{id}");
            }

            conf.StartDate = startDate;
            conf.EndDate = endDate;
            conf.AbstractSubmissionDeadline = abstractDeadline;
            conf.FullTextSubmissionDeadline = fullTextDeadline;

            await _context.SaveChangesAsync();
            return Redirect($"/{slug}/Admin/ConferenceWizard/Step3/{id}");
        }

        // ── Adım 3: Kayıt Tipleri ────────────────────────────────────────────

        [HttpGet("/{slug}/Admin/ConferenceWizard/Step3/{id:guid}")]
        public async Task<IActionResult> Step3(string slug, Guid id)
        {
            var conf = await _context.Conferences.FindAsync(id);
            if (conf == null) return NotFound();

            var types = await _context.RegistrationTypes.AsNoTracking()
                .Where(r => r.ConferenceId == id).OrderBy(r => r.Name).ToListAsync();

            ViewBag.Slug = slug;
            ViewBag.ConferenceId = id;
            ViewBag.ConferenceTitle = conf.Title;
            return View(types);
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step3/{id:guid}/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step3AddType(string slug, Guid id,
            string name, string? nameEn, decimal price, string currency, DateTime? deadline)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _context.RegistrationTypes.Add(new RegistrationType
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = id,
                    Name = name.Trim(),
                    NameEn = nameEn?.Trim(),
                    Description = string.Empty,
                    Price = price,
                    Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency,
                    Deadline = deadline,
                    IsActive = true,
                    RoleName = "Author"
                });
                await _context.SaveChangesAsync();
            }
            return Redirect($"/{slug}/Admin/ConferenceWizard/Step3/{id}");
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step3/{id:guid}/Delete/{typeId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step3DeleteType(string slug, Guid id, Guid typeId)
        {
            var rt = await _context.RegistrationTypes.FindAsync(typeId);
            if (rt != null && rt.ConferenceId == id)
            {
                _context.RegistrationTypes.Remove(rt);
                await _context.SaveChangesAsync();
            }
            return Redirect($"/{slug}/Admin/ConferenceWizard/Step3/{id}");
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step3/{id:guid}/Next")]
        [ValidateAntiForgeryToken]
        public IActionResult Step3Next(string slug, Guid id) =>
            Redirect($"/{slug}/Admin/ConferenceWizard/Step4/{id}");

        // ── Adım 4: Bildiri Konuları ─────────────────────────────────────────

        [HttpGet("/{slug}/Admin/ConferenceWizard/Step4/{id:guid}")]
        public async Task<IActionResult> Step4(string slug, Guid id)
        {
            var conf = await _context.Conferences.FindAsync(id);
            if (conf == null) return NotFound();

            var topics = await _context.ConferenceTopics.AsNoTracking()
                .Where(t => t.ConferenceId == id).OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();

            ViewBag.Slug = slug;
            ViewBag.ConferenceId = id;
            ViewBag.ConferenceTitle = conf.Title;
            return View(topics);
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step4/{id:guid}/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step4AddTopic(string slug, Guid id, string name, string? nameEn)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _context.ConferenceTopics.Add(new ConferenceTopic
                {
                    Id = Guid.NewGuid(),
                    ConferenceId = id,
                    Name = name.Trim(),
                    NameEn = nameEn?.Trim(),
                    IsActive = true,
                    SortOrder = 0
                });
                await _context.SaveChangesAsync();
            }
            return Redirect($"/{slug}/Admin/ConferenceWizard/Step4/{id}");
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step4/{id:guid}/Delete/{topicId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step4DeleteTopic(string slug, Guid id, Guid topicId)
        {
            var topic = await _context.ConferenceTopics.FindAsync(topicId);
            if (topic != null && topic.ConferenceId == id)
            {
                _context.ConferenceTopics.Remove(topic);
                await _context.SaveChangesAsync();
            }
            return Redirect($"/{slug}/Admin/ConferenceWizard/Step4/{id}");
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step4/{id:guid}/Next")]
        [ValidateAntiForgeryToken]
        public IActionResult Step4Next(string slug, Guid id) =>
            Redirect($"/{slug}/Admin/ConferenceWizard/Step5/{id}");

        // ── Adım 5: Ödeme Ayarları ───────────────────────────────────────────

        [HttpGet("/{slug}/Admin/ConferenceWizard/Step5/{id:guid}")]
        public async Task<IActionResult> Step5(string slug, Guid id)
        {
            var conf = await _context.Conferences.FindAsync(id);
            if (conf == null) return NotFound();
            ViewBag.Slug = slug;
            return View(conf);
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step5/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step5Post(string slug, Guid id,
            bool isStripeEnabled, bool isPayTREnabled, bool isBankTransferEnabled,
            string? bankName, string? bankIban, string? bankAccountName, string? bankBranch)
        {
            var conf = await _context.Conferences.FindAsync(id);
            if (conf == null) return NotFound();

            conf.IsStripeEnabled = isStripeEnabled;
            conf.IsPayTREnabled = isPayTREnabled;
            conf.IsBankTransferEnabled = isBankTransferEnabled;
            conf.BankName = bankName?.Trim();
            conf.BankIban = bankIban?.Trim();
            conf.BankAccountName = bankAccountName?.Trim();
            conf.BankBranch = bankBranch?.Trim();

            await _context.SaveChangesAsync();
            return Redirect($"/{slug}/Admin/ConferenceWizard/Step6/{id}");
        }

        // ── Adım 6: Yayına Hazırlık Checklist ───────────────────────────────

        [HttpGet("/{slug}/Admin/ConferenceWizard/Step6/{id:guid}")]
        public async Task<IActionResult> Step6(string slug, Guid id)
        {
            var conf = await _context.Conferences
                .AsNoTracking()
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (conf == null) return NotFound();

            var hasRegistrationTypes = await _context.RegistrationTypes
                .AnyAsync(r => r.ConferenceId == id);
            var hasTopics = await _context.ConferenceTopics
                .AnyAsync(t => t.ConferenceId == id);
            var hasPayment = conf.IsStripeEnabled || conf.IsPayTREnabled || conf.IsBankTransferEnabled;

            ViewBag.Slug = slug;
            ViewBag.HasRegistrationTypes = hasRegistrationTypes;
            ViewBag.HasTopics = hasTopics;
            ViewBag.HasPayment = hasPayment;
            return View(conf);
        }

        [HttpPost("/{slug}/Admin/ConferenceWizard/Step6/{id:guid}/Publish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(string slug, Guid id, bool openSubmission, bool openRegistration)
        {
            var conf = await _context.Conferences.FindAsync(id);
            if (conf == null) return NotFound();

            conf.IsSubmissionOpen = openSubmission;
            conf.IsRegistrationOpen = openRegistration;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"'{conf.Title}' kongresi başarıyla yayına alındı!";
            return Redirect($"/{slug}/Admin/ConferenceFlow?conferenceId={id}");
        }
    }
}
