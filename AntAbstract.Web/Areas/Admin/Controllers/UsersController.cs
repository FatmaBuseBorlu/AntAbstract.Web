using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("/Admin/Users")]
        public async Task<IActionResult> Index()
        {
            await EnsureBaseRoles();

            var users = await _userManager.Users.AsNoTracking().ToListAsync();
            var vm = new List<UserListItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                vm.Add(new UserListItemViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Name = $"{user.FirstName} {user.LastName}".Trim(),
                    Roles = roles ?? new List<string>()
                });
            }

            return View(vm);
        }

        [HttpGet("/{slug}/Admin/Users")]
        public IActionResult LegacyTenant(string slug) => Redirect("/Admin/Users");

        [HttpGet("/Admin/Users/ManageRoles")]
        public async Task<IActionResult> ManageRoles(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return NotFound();

            await EnsureBaseRoles();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var allRoles = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new UserWithRolesViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? "",
                Roles = new List<UserWithRoleViewModel>()
            };

            foreach (var role in allRoles)
            {
                var roleName = role.Name ?? "";
                if (string.IsNullOrWhiteSpace(roleName))
                    continue;

                model.Roles.Add(new UserWithRoleViewModel
                {
                    RoleName = roleName,
                    IsSelected = userRoles != null && userRoles.Contains(roleName)
                });
            }

            return View(model);
        }

        [HttpGet("/{slug}/Admin/Users/ManageRoles")]
        public IActionResult LegacyTenantManageRoles(string slug, string userId) => Redirect($"/Admin/Users/ManageRoles?userId={userId}");

        [HttpPost("/Admin/Users/ManageRoles")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(UserWithRolesViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserId))
                return BadRequest();

            model.Roles ??= new List<UserWithRoleViewModel>();

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return NotFound();

            var existingRoles = await _userManager.GetRolesAsync(user);
            existingRoles ??= new List<string>();

            var selectedRoles = model.Roles
                .Where(x => x.IsSelected && !string.IsNullOrWhiteSpace(x.RoleName))
                .Select(x => x.RoleName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rolesToRemove = existingRoles
                .Where(r => !selectedRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var rolesToAdd = selectedRoles
                .Where(r => !existingRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (rolesToRemove.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Rol kaldırma sırasında hata oluştu.";
                    return View(model);
                }
            }

            if (rolesToAdd.Count > 0)
            {
                foreach (var roleName in rolesToAdd)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                        await _roleManager.CreateAsync(new IdentityRole(roleName));
                }

                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Rol atama sırasında hata oluştu.";
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = "Roller güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/AssignRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "Kullanıcı veya rol bilgisi eksik.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createRoleResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Rol oluşturulamadı.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                var addResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Rol atama başarısız.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = "Rol ataması yapıldı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/RemoveRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "Kullanıcı veya rol bilgisi eksik.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                var removeResult = await _userManager.RemoveFromRoleAsync(user, roleName);
                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Rol kaldırma başarısız.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = "Rol kaldırıldı.";
            return RedirectToAction(nameof(Index));
        }

        private async Task EnsureBaseRoles()
        {
            var baseRoles = new[] { "Admin", "Organizator", "Author", "Referee" };

            foreach (var role in baseRoles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
