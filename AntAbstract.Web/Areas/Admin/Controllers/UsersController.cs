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
    [Authorize(Roles = "Admin,Organizator")]
    public class UsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IStringLocalizer<UsersController> _localizer;

        public UsersController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IStringLocalizer<UsersController> localizer)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _localizer = localizer;
        }

        [HttpGet("/Admin/Users")]
        [HttpGet("/{slug}/Admin/Users")]
        public async Task<IActionResult> Index()
        {
            await EnsureBaseRoles();

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null &&
                          await _userManager.IsInRoleAsync(currentUser, "Admin");

            var query = _userManager.Users
                .AsNoTracking()
                .AsQueryable();

            if (!isAdmin && currentUser?.TenantId != null)
            {
                query = query.Where(u => u.TenantId == currentUser.TenantId);
            }
            else if (!isAdmin && currentUser?.TenantId == null)
            {
                query = query.Where(u => false);
            }

            var users = await query.ToListAsync();

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

        [HttpGet("/Admin/Users/ManageRoles")]
        [HttpGet("/{slug}/Admin/Users/ManageRoles")]
        public async Task<IActionResult> ManageRoles(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return NotFound();
            }

            await EnsureBaseRoles();

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null &&
                          await _userManager.IsInRoleAsync(currentUser, "Admin");

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            if (!isAdmin && user.TenantId != currentUser?.TenantId)
            {
                TempData["ErrorMessage"] = _localizer["Error_UnauthorizedManageUser"].Value;
                return RedirectToAction(nameof(Index));
            }

            var allRoles = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            if (!isAdmin)
            {
                allRoles = allRoles
                    .Where(r => r.Name != "Admin")
                    .ToList();
            }

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
                {
                    continue;
                }

                model.Roles.Add(new UserWithRoleViewModel
                {
                    RoleName = roleName,
                    IsSelected = userRoles != null && userRoles.Contains(roleName)
                });
            }

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

            model.Roles ??= new List<UserWithRoleViewModel>();

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null &&
                          await _userManager.IsInRoleAsync(currentUser, "Admin");

            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user == null)
            {
                return NotFound();
            }

            if (!isAdmin && user.TenantId != currentUser?.TenantId)
            {
                TempData["ErrorMessage"] = _localizer["Error_UnauthorizedAction"].Value;
                return RedirectToAction(nameof(Index));
            }

            var existingRoles = await _userManager.GetRolesAsync(user);
            existingRoles ??= new List<string>();

            var selectedRoles = model.Roles
                .Where(x => x.IsSelected && !string.IsNullOrWhiteSpace(x.RoleName))
                .Select(x => x.RoleName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!isAdmin && selectedRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
            {
                selectedRoles.RemoveAll(r =>
                    r.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            }

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
                    TempData["ErrorMessage"] = _localizer["Error_RemoveRoleFailed"].Value;
                    return View(model);
                }
            }

            if (rolesToAdd.Count > 0)
            {
                foreach (var roleName in rolesToAdd)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }

                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);

                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = _localizer["Error_AssignRoleFailed"].Value;
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = _localizer["Success_RolesUpdated"].Value;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/AssignRole")]
        [HttpPost("/{slug}/Admin/Users/AssignRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = _localizer["Error_MissingUserOrRole"].Value;
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null &&
                          await _userManager.IsInRoleAsync(currentUser, "Admin");

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_UserNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin && user.TenantId != currentUser?.TenantId)
            {
                TempData["ErrorMessage"] = _localizer["Error_UnauthorizedAction"].Value;
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin && roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = _localizer["Error_CannotAssignSuperRole"].Value;
                return RedirectToAction(nameof(Index));
            }

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));

                if (!createRoleResult.Succeeded)
                {
                    TempData["ErrorMessage"] = _localizer["Error_RoleCouldNotBeCreated"].Value;
                    return RedirectToAction(nameof(Index));
                }
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                var addResult = await _userManager.AddToRoleAsync(user, roleName);

                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = _localizer["Error_RoleAssignmentFailed"].Value;
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = _localizer["Success_RoleAssigned"].Value;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Admin/Users/RemoveRole")]
        [HttpPost("/{slug}/Admin/Users/RemoveRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = _localizer["Error_MissingUserOrRole"].Value;
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null &&
                          await _userManager.IsInRoleAsync(currentUser, "Admin");

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = _localizer["Error_UserNotFound"].Value;
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin && user.TenantId != currentUser?.TenantId)
            {
                TempData["ErrorMessage"] = _localizer["Error_UnauthorizedAction"].Value;
                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin && roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = _localizer["Error_CannotRemoveSuperRole"].Value;
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                var removeResult = await _userManager.RemoveFromRoleAsync(user, roleName);

                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = _localizer["Error_RoleRemovalFailed"].Value;
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = _localizer["Success_RoleRemoved"].Value;

            return RedirectToAction(nameof(Index));
        }

        private async Task EnsureBaseRoles()
        {
            var baseRoles = new[]
            {
                "Admin",
                "Organizator",
                "Author",
                "Referee"
            };

            foreach (var role in baseRoles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}