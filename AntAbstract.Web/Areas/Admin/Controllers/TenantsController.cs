using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels.Admin.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
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

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private static string NormalizeSlug(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return "";
            }

            var text = slug.Trim().ToLowerInvariant();

            text = text
                .Replace("ş", "s")
                .Replace("ı", "i")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c");

            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            text = Regex.Replace(text, @"\s+", "-").Trim('-');

            return text;
        }

        private static bool IsValidSlug(string slug)
        {
            return Regex.IsMatch(slug, @"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        }

        private async Task FillSelectListsAsync(
            int? selectedScientificFieldId = null,
            int? selectedCongressTypeId = null)
        {
            var scientificFields = await _context.ScientificFields
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            var congressTypes = await _context.CongressTypes
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.ScientificFieldId = new SelectList(
                scientificFields,
                "Id",
                "Name",
                selectedScientificFieldId);

            ViewBag.CongressTypeId = new SelectList(
                congressTypes,
                "Id",
                "Name",
                selectedCongressTypeId);
        }

        private async Task FillAssignableAdminUsersAsync(TenantAssignManagerViewModel model)
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .ThenBy(x => x.Email)
                .ToListAsync();

            var tenants = await _context.Tenants
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            model.AvailableUsers = new List<SelectListItem>();

            foreach (var user in users)
            {
                if (user == null || string.IsNullOrWhiteSpace(user.Id))
                {
                    continue;
                }

                var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");

                if (isSuperAdmin)
                {
                    continue;
                }

                var fullName = $"{user.FirstName} {user.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = user.Email ?? user.UserName ?? "Kullanıcı";
                }

                var currentTenantText = "Kurumsuz";

                if (user.TenantId.HasValue &&
                    tenants.TryGetValue(user.TenantId.Value, out var tenantName))
                {
                    currentTenantText = tenantName;
                }

                var roles = await _userManager.GetRolesAsync(user);

                var roleText = roles.Any()
                    ? string.Join(", ", roles)
                    : "Rol yok";

                model.AvailableUsers.Add(new SelectListItem
                {
                    Value = user.Id,
                    Text = $"{fullName} - {user.Email} | {roleText} | {currentTenantText}",
                    Selected = user.Id == model.ExistingUserId
                });
            }
        }

        public async Task<IActionResult> Index()
        {
            var tenants = await _context.Tenants
                .AsNoTracking()
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType)
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(tenants);
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants
                .AsNoTracking()
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType)
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (tenant == null)
            {
                return NotFound();
            }

            return View(tenant);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await FillSelectListsAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Slug,LogoUrl,ScientificFieldId,CongressTypeId")] Tenant tenant)
        {
            ModelState.Remove("Users");
            ModelState.Remove("ScientificField");
            ModelState.Remove("CongressType");

            tenant.Slug = NormalizeSlug(tenant.Slug);

            if (string.IsNullOrWhiteSpace(tenant.Name))
            {
                ModelState.AddModelError(
                    nameof(tenant.Name),
                    T("Error_NameRequired", "Kurum adı zorunludur."));
            }

            if (string.IsNullOrWhiteSpace(tenant.Slug))
            {
                ModelState.AddModelError(
                    nameof(tenant.Slug),
                    T("Error_SlugRequired", "Slug zorunludur."));
            }
            else if (!IsValidSlug(tenant.Slug))
            {
                ModelState.AddModelError(
                    nameof(tenant.Slug),
                    T("Error_InvalidSlug", "Slug sadece küçük harf, rakam ve tire içerebilir."));
            }

            var slugExists = await _context.Tenants
                .AsNoTracking()
                .AnyAsync(x => x.Slug.ToLower() == tenant.Slug.ToLower());

            if (slugExists)
            {
                ModelState.AddModelError(
                    nameof(tenant.Slug),
                    T("Error_SlugAlreadyExists", "Bu slug zaten kullanılıyor."));
            }

            if (!ModelState.IsValid)
            {
                await FillSelectListsAsync(
                    tenant.ScientificFieldId,
                    tenant.CongressTypeId);

                return View(tenant);
            }

            tenant.Id = Guid.NewGuid();

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_TenantCreated",
                "Kurum başarıyla oluşturuldu.");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(x => x.Id == id.Value);

            if (tenant == null)
            {
                return NotFound();
            }

            await FillSelectListsAsync(
                tenant.ScientificFieldId,
                tenant.CongressTypeId);

            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Guid id,
            [Bind("Id,Name,Slug,LogoUrl,ScientificFieldId,CongressTypeId")] Tenant tenant)
        {
            if (id != tenant.Id)
            {
                return NotFound();
            }

            ModelState.Remove("Users");
            ModelState.Remove("ScientificField");
            ModelState.Remove("CongressType");

            tenant.Slug = NormalizeSlug(tenant.Slug);

            if (string.IsNullOrWhiteSpace(tenant.Name))
            {
                ModelState.AddModelError(
                    nameof(tenant.Name),
                    T("Error_NameRequired", "Kurum adı zorunludur."));
            }

            if (string.IsNullOrWhiteSpace(tenant.Slug))
            {
                ModelState.AddModelError(
                    nameof(tenant.Slug),
                    T("Error_SlugRequired", "Slug zorunludur."));
            }
            else if (!IsValidSlug(tenant.Slug))
            {
                ModelState.AddModelError(
                    nameof(tenant.Slug),
                    T("Error_InvalidSlug", "Slug sadece küçük harf, rakam ve tire içerebilir."));
            }

            var slugExists = await _context.Tenants
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Slug.ToLower() == tenant.Slug.ToLower() &&
                    x.Id != tenant.Id);

            if (slugExists)
            {
                ModelState.AddModelError(
                    nameof(tenant.Slug),
                    T("Error_SlugAlreadyExists", "Bu slug zaten kullanılıyor."));
            }

            if (!ModelState.IsValid)
            {
                await FillSelectListsAsync(
                    tenant.ScientificFieldId,
                    tenant.CongressTypeId);

                return View(tenant);
            }

            var existingTenant = await _context.Tenants
                .FirstOrDefaultAsync(x => x.Id == tenant.Id);

            if (existingTenant == null)
            {
                return NotFound();
            }

            existingTenant.Name = tenant.Name;
            existingTenant.Slug = tenant.Slug;
            existingTenant.LogoUrl = tenant.LogoUrl;
            existingTenant.ScientificFieldId = tenant.ScientificFieldId;
            existingTenant.CongressTypeId = tenant.CongressTypeId;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_TenantUpdated",
                "Kurum başarıyla güncellendi.");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                return NotFound();
            }

            var tenant = await _context.Tenants
                .AsNoTracking()
                .Include(t => t.ScientificField)
                .Include(t => t.CongressType)
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (tenant == null)
            {
                return NotFound();
            }

            return View(tenant);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(x => x.Id == id);

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
                TempData["ErrorMessage"] = T(
                    "Error_TenantHasDependencies",
                    "Bu kuruma bağlı kongre, kullanıcı veya sayfa içerikleri olduğu için silinemez. Önce bağlı kayıtları temizleyin.");

                return RedirectToAction(nameof(Index));
            }

            _context.Tenants.Remove(tenant);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = T(
                "Success_TenantDeleted",
                "Kurum başarıyla silindi.");

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
public async Task<IActionResult> AssignManager(Guid id, string? returnUrl = null)
{
    if (id == Guid.Empty)
    {
        return NotFound();
    }

    var tenant = await _context.Tenants
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (tenant == null)
    {
        return NotFound();
    }

    var model = new TenantAssignManagerViewModel
    {
        TenantId = tenant.Id,
        TenantName = tenant.Name,
        AssignmentMode = "Existing",
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
    };

    await FillAssignableAdminUsersAsync(model);

    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AssignManager(TenantAssignManagerViewModel model)
{
    var tenant = await _context.Tenants
        .FirstOrDefaultAsync(x => x.Id == model.TenantId);

    if (tenant == null)
    {
        return NotFound();
    }

    model.TenantName = tenant.Name;

    var safeReturnUrl = !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
        ? model.ReturnUrl
        : null;

    var assignmentMode = string.IsNullOrWhiteSpace(model.AssignmentMode)
        ? "Existing"
        : model.AssignmentMode;

    if (assignmentMode == "Existing")
    {
        ModelState.Remove(nameof(model.FirstName));
        ModelState.Remove(nameof(model.LastName));
        ModelState.Remove(nameof(model.Email));
        ModelState.Remove(nameof(model.Password));

        if (string.IsNullOrWhiteSpace(model.ExistingUserId))
        {
            ModelState.AddModelError(
                nameof(model.ExistingUserId),
                T("Error_SelectUserRequired", "Lütfen admin yapılacak kullanıcıyı seçiniz."));
        }

        if (!ModelState.IsValid)
        {
            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        var existingUser = await _userManager.FindByIdAsync(model.ExistingUserId!);

        if (existingUser == null)
        {
            ModelState.AddModelError(
                nameof(model.ExistingUserId),
                T("Error_UserNotFound", "Seçilen kullanıcı bulunamadı."));

            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        var isSuperAdmin = await _userManager.IsInRoleAsync(existingUser, "SuperAdmin");

        if (isSuperAdmin)
        {
            ModelState.AddModelError(
                nameof(model.ExistingUserId),
                T("Error_SuperAdminCannotBeTenantAdmin", "Süper Admin kullanıcıları kurum admini olarak atanamaz."));

            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        existingUser.TenantId = tenant.Id;
        existingUser.EmailConfirmed = true;

        var updateResult = await _userManager.UpdateAsync(existingUser);

        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync("Admin"))
        {
            await _roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        if (!await _userManager.IsInRoleAsync(existingUser, "Admin"))
        {
            var addRoleResult = await _userManager.AddToRoleAsync(existingUser, "Admin");

            if (!addRoleResult.Succeeded)
            {
                foreach (var error in addRoleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                model.ReturnUrl = safeReturnUrl;
                await FillAssignableAdminUsersAsync(model);
                return View(model);
            }
        }

        var fullName = $"{existingUser.FirstName} {existingUser.LastName}".Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = existingUser.Email ?? existingUser.UserName ?? "Kullanıcı";
        }

        TempData["SuccessMessage"] = $"{tenant.Name} kurumuna {fullName} adlı kullanıcı admin olarak atandı.";

        if (!string.IsNullOrWhiteSpace(safeReturnUrl))
        {
            return LocalRedirect(safeReturnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    if (assignmentMode == "New")
    {
        ModelState.Remove(nameof(model.ExistingUserId));

        if (string.IsNullOrWhiteSpace(model.FirstName))
        {
            ModelState.AddModelError(
                nameof(model.FirstName),
                T("Error_FirstNameRequired", "Ad alanı zorunludur."));
        }

        if (string.IsNullOrWhiteSpace(model.LastName))
        {
            ModelState.AddModelError(
                nameof(model.LastName),
                T("Error_LastNameRequired", "Soyad alanı zorunludur."));
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(
                nameof(model.Email),
                T("Error_EmailRequired", "E-posta alanı zorunludur."));
        }

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(
                nameof(model.Password),
                T("Error_PasswordRequired", "Şifre zorunludur."));
        }

        if (!ModelState.IsValid)
        {
            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email!);

        if (existingUser != null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                T("Error_EmailAlreadyRegistered", "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var. Mevcut kullanıcıyı seçerek admin yapabilirsiniz."));

            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        var adminUser = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            TenantId = model.TenantId,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(adminUser, model.Password!);

        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync("Admin"))
        {
            await _roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(adminUser, "Admin");

        if (!addRoleResult.Succeeded)
        {
            foreach (var error in addRoleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            model.ReturnUrl = safeReturnUrl;
            await FillAssignableAdminUsersAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = $"{tenant.Name} kurumuna {model.FirstName} {model.LastName} adlı yeni admin oluşturuldu.";

        if (!string.IsNullOrWhiteSpace(safeReturnUrl))
        {
            return LocalRedirect(safeReturnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    ModelState.AddModelError(
        nameof(model.AssignmentMode),
        T("Error_InvalidAssignmentMode", "Geçersiz admin atama yöntemi."));

    model.ReturnUrl = safeReturnUrl;

    await FillAssignableAdminUsersAsync(model);

    return View(model);
}

        [HttpGet]
        public IActionResult AddConference(Guid id)
        {
            TempData["ErrorMessage"] = T(
                "Error_ConferenceCreateFromTenantDisabled",
                "Kongre oluşturma işlemi kurum admini tarafından Kongreler ekranından yapılmalıdır.");

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddConference(Guid id, Conference conference)
        {
            TempData["ErrorMessage"] = T(
                "Error_ConferenceCreateFromTenantDisabled",
                "Kongre oluşturma işlemi kurum admini tarafından Kongreler ekranından yapılmalıdır.");

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}