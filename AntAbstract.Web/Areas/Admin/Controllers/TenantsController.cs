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

        private static readonly string[] AssignableTenantRoles =
        {
            "Admin",
            "Author",
            "Referee",
            "Listener"
        };

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

        private string? GetSafeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return null;
            }

            if (Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
            {
                var requestHost = Request.Host.Value;

                var isSameHost = string.Equals(
                    absoluteUri.Authority,
                    requestHost,
                    StringComparison.OrdinalIgnoreCase);

                if (isSameHost)
                {
                    return absoluteUri.PathAndQuery;
                }
            }

            return null;
        }

        private IActionResult RedirectBackOrIndex(string? returnUrl)
        {
            var safeReturnUrl = GetSafeReturnUrl(returnUrl);

            if (!string.IsNullOrWhiteSpace(safeReturnUrl))
            {
                return LocalRedirect(safeReturnUrl);
            }

            return RedirectToAction(nameof(Index));
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

        private static string GetRoleDisplayName(string role)
        {
            return role switch
            {
                "Admin" => "Admin",
                "Author" => "Yazar",
                "Referee" => "Hakem",
                "Listener" => "Dinleyici",
                _ => role
            };
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

        private void FillAssignableTenantRoles(TenantAssignUserViewModel model)
        {
            model.AvailableRoles = AssignableTenantRoles
                .Select(role => new SelectListItem
                {
                    Value = role,
                    Text = GetRoleDisplayName(role),
                    Selected = string.Equals(
                        role,
                        model.SelectedRole,
                        StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
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

        private async Task FillAssignableTenantUsersAsync(TenantAssignUserViewModel model)
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
                    ? string.Join(", ", roles.Select(GetRoleDisplayName))
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

            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .ThenBy(x => x.Email)
                .ToListAsync();

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.TenantId
                })
                .ToListAsync();

            var adminUsers = new List<AppUser>();

            if (await _roleManager.RoleExistsAsync("Admin"))
            {
                adminUsers = (await _userManager.GetUsersInRoleAsync("Admin")).ToList();
            }

            var adminUserIds = adminUsers
                .Select(x => x.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var model = tenants.Select(tenant =>
            {
                var tenantUsers = users
                    .Where(x => x.TenantId == tenant.Id)
                    .ToList();

                var adminNames = tenantUsers
                    .Where(x => adminUserIds.Contains(x.Id))
                    .Select(x =>
                    {
                        var fullName = $"{x.FirstName} {x.LastName}".Trim();

                        return string.IsNullOrWhiteSpace(fullName)
                            ? x.Email ?? x.UserName ?? "Admin"
                            : fullName;
                    })
                    .OrderBy(x => x)
                    .ToList();

                return new TenantListItemViewModel
                {
                    Id = tenant.Id,
                    Name = tenant.Name,
                    Slug = tenant.Slug,
                    ScientificFieldName = tenant.ScientificField?.Name,
                    CongressTypeName = tenant.CongressType?.Name,
                    ConferenceCount = conferences.Count(x => x.TenantId == tenant.Id),
                    UserCount = tenantUsers.Count,
                    AdminNames = adminNames
                };
            }).ToList();

            ViewBag.TotalTenantCount = model.Count;
            ViewBag.TenantWithAdminCount = model.Count(x => x.HasAdmin);
            ViewBag.TotalConferenceCount = model.Sum(x => x.ConferenceCount);
            ViewBag.TotalUserCount = model.Sum(x => x.UserCount);

            return View(model);
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

            var conferences = await _context.Conferences
                .AsNoTracking()
                .Where(x => x.TenantId == tenant.Id)
                .OrderByDescending(x => x.StartDate)
                .Select(x => new TenantDetailConferenceViewModel
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    StartDate = x.StartDate,
                    StatusText = "Aktif"
                })
                .ToListAsync();

            var firstConferenceId = conferences
                .Select(x => x.Id)
                .FirstOrDefault();

            Guid? conferenceId = firstConferenceId == Guid.Empty
                ? null
                : firstConferenceId;

            var conferenceFlowUrl = conferenceId.HasValue && !string.IsNullOrWhiteSpace(tenant.Slug)
                ? $"/{tenant.Slug}/Admin/ConferenceFlow?conferenceId={conferenceId.Value}"
                : null;

            var detailsReturnUrl = Url.Action(
                action: nameof(Details),
                controller: "Tenants",
                values: new { area = "Admin", id = tenant.Id });

            var tenantUsers = await _context.Users
                .AsNoTracking()
                .Where(x => x.TenantId == tenant.Id)
                .OrderBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .ThenBy(x => x.Email)
                .ToListAsync();

            var userModels = new List<TenantDetailUserViewModel>();

            foreach (var tenantUser in tenantUsers)
            {
                var roles = await _userManager.GetRolesAsync(tenantUser);

                var fullName = $"{tenantUser.FirstName} {tenantUser.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = tenantUser.Email ?? tenantUser.UserName ?? "Kullanıcı";
                }

                userModels.Add(new TenantDetailUserViewModel
                {
                    UserId = tenantUser.Id,
                    DisplayName = fullName,
                    Email = tenantUser.Email,
                    Roles = roles
                        .OrderBy(x => x)
                        .ToList()
                });
            }

            var admins = userModels
                .Where(x => x.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
                .OrderBy(x => x.DisplayName)
                .ToList();

            var model = new TenantDetailViewModel
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Slug = tenant.Slug,
                LogoUrl = tenant.LogoUrl,
                ScientificFieldName = tenant.ScientificField?.Name,
                CongressTypeName = tenant.CongressType?.Name,
                ConferenceFlowUrl = conferenceFlowUrl,
                AssignManagerReturnUrl = detailsReturnUrl,
                Admins = admins,
                Users = userModels,
                Conferences = conferences
            };

            return View(model);
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

            var safeReturnUrl = GetSafeReturnUrl(returnUrl);

            var model = new TenantAssignManagerViewModel
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                AssignmentMode = "Existing",
                ReturnUrl = safeReturnUrl
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

            var safeReturnUrl = GetSafeReturnUrl(model.ReturnUrl);

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

                return RedirectBackOrIndex(safeReturnUrl);
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

                return RedirectBackOrIndex(safeReturnUrl);
            }

            ModelState.AddModelError(
                nameof(model.AssignmentMode),
                T("Error_InvalidAssignmentMode", "Geçersiz admin atama yöntemi."));

            model.ReturnUrl = safeReturnUrl;

            await FillAssignableAdminUsersAsync(model);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AssignUser(Guid id, string? returnUrl = null)
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

            var safeReturnUrl = GetSafeReturnUrl(returnUrl);

            var model = new TenantAssignUserViewModel
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                AssignmentMode = "Existing",
                SelectedRole = "Referee",
                ReturnUrl = safeReturnUrl
            };

            FillAssignableTenantRoles(model);
            await FillAssignableTenantUsersAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignUser(TenantAssignUserViewModel model)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(x => x.Id == model.TenantId);

            if (tenant == null)
            {
                return NotFound();
            }

            model.TenantName = tenant.Name;

            var safeReturnUrl = GetSafeReturnUrl(model.ReturnUrl);

            var assignmentMode = string.IsNullOrWhiteSpace(model.AssignmentMode)
                ? "Existing"
                : model.AssignmentMode;

            var selectedRole = model.SelectedRole?.Trim();

            var roleName = AssignableTenantRoles.FirstOrDefault(role =>
                string.Equals(role, selectedRole, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError(
                    nameof(model.SelectedRole),
                    T("Error_SelectRoleRequired", "Lütfen kullanıcıya verilecek rolü seçiniz."));
            }

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
                        T("Error_SelectUserRequired", "Lütfen kuruma bağlanacak kullanıcıyı seçiniz."));
                }

                if (!ModelState.IsValid)
                {
                    model.ReturnUrl = safeReturnUrl;

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

                    return View(model);
                }

                var existingUser = await _userManager.FindByIdAsync(model.ExistingUserId!);

                if (existingUser == null)
                {
                    ModelState.AddModelError(
                        nameof(model.ExistingUserId),
                        T("Error_UserNotFound", "Seçilen kullanıcı bulunamadı."));

                    model.ReturnUrl = safeReturnUrl;

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

                    return View(model);
                }

                var isSuperAdmin = await _userManager.IsInRoleAsync(existingUser, "SuperAdmin");

                if (isSuperAdmin)
                {
                    ModelState.AddModelError(
                        nameof(model.ExistingUserId),
                        T("Error_SuperAdminCannotBeAssignedToTenant", "Süper Admin kullanıcıları kuruma bu ekrandan bağlanamaz."));

                    model.ReturnUrl = safeReturnUrl;

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

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

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

                    return View(model);
                }

                if (!await _roleManager.RoleExistsAsync(roleName!))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName!));
                }

                if (!await _userManager.IsInRoleAsync(existingUser, roleName!))
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(existingUser, roleName!);

                    if (!addRoleResult.Succeeded)
                    {
                        foreach (var error in addRoleResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        model.ReturnUrl = safeReturnUrl;

                        FillAssignableTenantRoles(model);
                        await FillAssignableTenantUsersAsync(model);

                        return View(model);
                    }
                }

                var fullName = $"{existingUser.FirstName} {existingUser.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = existingUser.Email ?? existingUser.UserName ?? "Kullanıcı";
                }

                TempData["SuccessMessage"] =
                    $"{tenant.Name} kurumuna {fullName} adlı kullanıcı {GetRoleDisplayName(roleName!)} rolüyle bağlandı.";

                return RedirectBackOrIndex(safeReturnUrl);
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

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

                    return View(model);
                }

                var existingUser = await _userManager.FindByEmailAsync(model.Email!);

                if (existingUser != null)
                {
                    ModelState.AddModelError(
                        nameof(model.Email),
                        T("Error_EmailAlreadyRegistered", "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var. Mevcut kullanıcıyı seçerek kuruma bağlayabilirsiniz."));

                    model.ReturnUrl = safeReturnUrl;

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

                    return View(model);
                }

                var newUser = new AppUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    TenantId = model.TenantId,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(newUser, model.Password!);

                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    model.ReturnUrl = safeReturnUrl;

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

                    return View(model);
                }

                if (!await _roleManager.RoleExistsAsync(roleName!))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName!));
                }

                var addRoleResult = await _userManager.AddToRoleAsync(newUser, roleName!);

                if (!addRoleResult.Succeeded)
                {
                    foreach (var error in addRoleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    model.ReturnUrl = safeReturnUrl;

                    FillAssignableTenantRoles(model);
                    await FillAssignableTenantUsersAsync(model);

                    return View(model);
                }

                TempData["SuccessMessage"] =
                    $"{tenant.Name} kurumuna {model.FirstName} {model.LastName} adlı yeni kullanıcı {GetRoleDisplayName(roleName!)} rolüyle oluşturuldu.";

                return RedirectBackOrIndex(safeReturnUrl);
            }

            ModelState.AddModelError(
                nameof(model.AssignmentMode),
                T("Error_InvalidAssignmentMode", "Geçersiz kullanıcı bağlama yöntemi."));

            model.ReturnUrl = safeReturnUrl;

            FillAssignableTenantRoles(model);
            await FillAssignableTenantUsersAsync(model);

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