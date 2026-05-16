using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels.Admin.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IStringLocalizer<UsersController> _localizer;

        private static readonly string[] AllowedRoles =
        {
            "SuperAdmin",
            "Admin",
            "Author",
            "Listener",
            "Referee"
        };

        public UsersController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IStringLocalizer<UsersController> localizer)
        {
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

        private static bool IsAllowedRole(string roleName)
        {
            return AllowedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
        }

        private async Task EnsureBaseRolesAsync()
        {
            foreach (var roleName in AllowedRoles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        [HttpGet("/Admin/Users")]
        [HttpGet("/{slug}/Admin/Users")]
        public async Task<IActionResult> Index()
        {
            await EnsureBaseRolesAsync();

            var users = await _userManager.Users
                .AsNoTracking()
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.Email)
                .ToListAsync();

            var model = new List<UserListItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                model.Add(new UserListItemViewModel
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Name = $"{user.FirstName} {user.LastName}".Trim(),
                    Roles = roles
                        .Where(IsAllowedRole)
                        .OrderBy(role => role)
                        .ToList()
                });
            }

            return View(model);
        }

        [HttpGet("/Admin/Users/ManageRoles")]
        [HttpGet("/{slug}/Admin/Users/ManageRoles")]
        public async Task<IActionResult> ManageRoles(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return NotFound();
            }

            await EnsureBaseRolesAsync();

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new UserWithRolesViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? "",
                Roles = AllowedRoles
                    .OrderBy(role => role)
                    .Select(role => new UserWithRoleViewModel
                    {
                        RoleName = role,
                        IsSelected = userRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost("/Admin/Users/ManageRoles")]
        [HttpPost("/{slug}/Admin/Users/ManageRoles")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(UserWithRolesViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserId))
            {
                return BadRequest();
            }

            await EnsureBaseRolesAsync();

            model.Roles ??= new List<UserWithRoleViewModel>();

            var targetUser = await _userManager.FindByIdAsync(model.UserId);

            if (targetUser == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var existingRoles = await _userManager.GetRolesAsync(targetUser);

            var selectedRoles = model.Roles
                .Where(x => x.IsSelected && !string.IsNullOrWhiteSpace(x.RoleName))
                .Select(x => x.RoleName.Trim())
                .Where(IsAllowedRole)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targetUser.Id == currentUser.Id &&
                !selectedRoles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotRemoveOwnSuperAdminRole",
                    "Kendi SuperAdmin rolünüzü kaldıramazsınız.");

                model.UserEmail = targetUser.Email ?? "";
                model.Roles = AllowedRoles
                    .OrderBy(role => role)
                    .Select(role => new UserWithRoleViewModel
                    {
                        RoleName = role,
                        IsSelected = existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
                    })
                    .ToList();

                return View(model);
            }

            var existingAllowedRoles = existingRoles
                .Where(IsAllowedRole)
                .ToList();

            var rolesToRemove = existingAllowedRoles
                .Where(role => !selectedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var rolesToAdd = selectedRoles
                .Where(role => !existingAllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (rolesToRemove.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);

                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RemoveRoleFailed",
                        "Rol kaldırılırken bir hata oluştu.");

                    model.UserEmail = targetUser.Email ?? "";
                    return View(model);
                }
            }

            if (rolesToAdd.Any())
            {
                var addResult = await _userManager.AddToRolesAsync(targetUser, rolesToAdd);

                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_AssignRoleFailed",
                        "Rol atanırken bir hata oluştu.");

                    model.UserEmail = targetUser.Email ?? "";
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = T(
                "Success_RolesUpdated",
                "Kullanıcı rolleri başarıyla güncellendi.");

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/AssignRole")]
        [HttpPost("/{slug}/Admin/Users/AssignRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_MissingUserOrRole",
                    "Kullanıcı veya rol bilgisi eksik.");

                return RedirectToAction(nameof(Index));
            }

            roleName = roleName.Trim();

            if (!IsAllowedRole(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidRole",
                    "Geçersiz rol seçimi.");

                return RedirectToAction(nameof(Index));
            }

            await EnsureBaseRolesAsync();

            var targetUser = await _userManager.FindByIdAsync(userId);

            if (targetUser == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserNotFound",
                    "Kullanıcı bulunamadı.");

                return RedirectToAction(nameof(Index));
            }

            if (!await _userManager.IsInRoleAsync(targetUser, roleName))
            {
                var addResult = await _userManager.AddToRoleAsync(targetUser, roleName);

                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RoleAssignmentFailed",
                        "Rol atanırken bir hata oluştu.");

                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = T(
                "Success_RoleAssigned",
                "Rol başarıyla atandı.");

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/RemoveRole")]
        [HttpPost("/{slug}/Admin/Users/RemoveRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_MissingUserOrRole",
                    "Kullanıcı veya rol bilgisi eksik.");

                return RedirectToAction(nameof(Index));
            }

            roleName = roleName.Trim();

            if (!IsAllowedRole(roleName))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidRole",
                    "Geçersiz rol seçimi.");

                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var targetUser = await _userManager.FindByIdAsync(userId);

            if (targetUser == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserNotFound",
                    "Kullanıcı bulunamadı.");

                return RedirectToAction(nameof(Index));
            }

            if (targetUser.Id == currentUser.Id &&
                roleName.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotRemoveOwnSuperAdminRole",
                    "Kendi SuperAdmin rolünüzü kaldıramazsınız.");

                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(targetUser, roleName))
            {
                var removeResult = await _userManager.RemoveFromRoleAsync(targetUser, roleName);

                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_RoleRemovalFailed",
                        "Rol kaldırılırken bir hata oluştu.");

                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = T(
                "Success_RoleRemoved",
                "Rol başarıyla kaldırıldı.");

            return RedirectToAction(nameof(Index));
        }
    }
}