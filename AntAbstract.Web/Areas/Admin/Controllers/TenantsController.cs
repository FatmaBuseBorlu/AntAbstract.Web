using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("Tenants/{action=Index}/{id?}")]
    [Route("Admin/Tenants/{action=Index}/{id?}")]
    public class TenantsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IStringLocalizer<TenantsController> _localizer;

        public TenantsController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IStringLocalizer<TenantsController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index()
        {
            var tenants = _context.Tenants
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType);

            return View(await tenants.ToListAsync());
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.Tenants
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tenant == null) return NotFound();

            return View(tenant);
        }

        public IActionResult Create()
        {
            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name");
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Slug,LogoUrl,ScientificFieldId,CongressTypeId")] Tenant tenant)
        {
            ModelState.Remove("Conferences");
            ModelState.Remove("Users");
            ModelState.Remove("ScientificField");
            ModelState.Remove("CongressType");

            if (ModelState.IsValid)
            {
                var slugExists = await _context.Tenants.AnyAsync(x => x.Slug.ToLower() == tenant.Slug.ToLower());
                if (slugExists)
                {
                    ModelState.AddModelError("Slug", _localizer["Error_SlugAlreadyExists"].Value);
                }
                else
                {
                    tenant.Id = Guid.NewGuid();
                    _context.Add(tenant);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = _localizer["Success_TenantCreated"].Value;
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name", tenant.ScientificFieldId);
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name", tenant.CongressTypeId);
            return View(tenant);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name", tenant.ScientificFieldId);
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name", tenant.CongressTypeId);
            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Slug,LogoUrl,ScientificFieldId,CongressTypeId")] Tenant tenant)
        {
            if (id != tenant.Id) return NotFound();

            ModelState.Remove("Conferences");
            ModelState.Remove("Users");
            ModelState.Remove("ScientificField");
            ModelState.Remove("CongressType");

            if (ModelState.IsValid)
            {
                var slugExists = await _context.Tenants.AnyAsync(x => x.Slug.ToLower() == tenant.Slug.ToLower() && x.Id != tenant.Id);
                if (slugExists)
                {
                    ModelState.AddModelError("Slug", _localizer["Error_SlugAlreadyExists"].Value);
                }
                else
                {
                    try
                    {
                        _context.Update(tenant);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = _localizer["Success_TenantUpdated"].Value;
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!TenantExists(tenant.Id)) return NotFound();
                        else throw;
                    }

                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.ScientificFieldId = new SelectList(_context.ScientificFields.OrderBy(s => s.Name), "Id", "Name", tenant.ScientificFieldId);
            ViewBag.CongressTypeId = new SelectList(_context.CongressTypes.OrderBy(c => c.Name), "Id", "Name", tenant.CongressTypeId);
            return View(tenant);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.Tenants
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tenant == null) return NotFound();

            return View(tenant);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var tenant = await _context.Tenants.FindAsync(id);

            if (tenant == null)
            {
                return NotFound();
            }

            var hasConferences = await _context.Conferences
                .AnyAsync(x => x.TenantId == id);

            var hasUsers = await _context.Users
                .AnyAsync(x => x.TenantId == id);

            var hasPageBlocks = await _context.ConferencePageBlocks
                .AnyAsync(x => x.TenantId == id);

            if (hasConferences || hasUsers || hasPageBlocks)
            {
                TempData["ErrorMessage"] =
                    "Bu kuruma bağlı kongre, kullanıcı veya sayfa içerikleri olduğu için silinemez. Önce bağlı kayıtları temizleyin.";

                return RedirectToAction(nameof(Index));
            }

            _context.Tenants.Remove(tenant);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = _localizer["Success_TenantDeleted"].Value;

            return RedirectToAction(nameof(Index));
        }

        private bool TenantExists(Guid id)
        {
            return _context.Tenants.Any(e => e.Id == id);
        }

        [HttpGet]
        public async Task<IActionResult> AssignManager(Guid id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            var model = new AntAbstract.Web.Models.ViewModels.Admin.Tenants.TenantAssignManagerViewModel
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignManager(AntAbstract.Web.Models.ViewModels.Admin.Tenants.TenantAssignManagerViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var tenant = await _context.Tenants.FindAsync(model.TenantId);
            if (tenant == null) return NotFound();

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", _localizer["Error_EmailAlreadyRegistered"].Value);
                model.TenantName = tenant.Name;
                return View(model);
            }

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                TenantId = model.TenantId,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Organizator"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Organizator"));
                }

                await _userManager.AddToRoleAsync(user, "Organizator");

                TempData["SuccessMessage"] = _localizer["Success_ManagerAssigned", tenant.Name, model.FirstName, model.LastName].Value;
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            model.TenantName = tenant.Name;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AddConference(Guid id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            ViewBag.TenantId = tenant.Id;
            ViewBag.TenantName = tenant.Name;

            var conference = new Conference { TenantId = tenant.Id };
            return View(conference);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConference(Guid id, Conference conference)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            ModelState.Remove("Tenant");
            ModelState.Remove("Sessions");
            ModelState.Remove("Submissions");
            ModelState.Remove("RegistrationTypes");

            if (ModelState.IsValid)
            {
                conference.Id = Guid.NewGuid();
                conference.TenantId = id;

                _context.Conferences.Add(conference);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = _localizer["Success_ConferenceAddedToTenant", tenant.Name].Value;
                return RedirectToAction(nameof(Details), new { id = id });
            }

            ViewBag.TenantId = tenant.Id;
            ViewBag.TenantName = tenant.Name;
            return View(conference);
        }
    }
}