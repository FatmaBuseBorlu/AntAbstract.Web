using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels.Admin.Referee;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Organizator")]
    [Route("Referee/{action=Index}/{id?}")]
    [Route("Admin/Referee/{action=Index}/{id?}")]
    public class RefereeController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IStringLocalizer<RefereeController> _localizer;

        public RefereeController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IStringLocalizer<RefereeController> localizer)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _localizer = localizer;
        }

        private string T(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
        }

        private async Task<bool> IsCurrentUserAdminAsync(AppUser? currentUser)
        {
            return currentUser != null &&
                   await _userManager.IsInRoleAsync(currentUser, "Admin");
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await IsCurrentUserAdminAsync(currentUser);

            var referees = await _userManager.GetUsersInRoleAsync("Referee");

            if (!isAdmin && currentUser?.TenantId != null)
            {
                referees = referees
                    .Where(r => r.TenantId == currentUser.TenantId)
                    .ToList();
            }
            else if (!isAdmin && currentUser?.TenantId == null)
            {
                referees = new List<AppUser>();
            }

            return View(referees);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await IsCurrentUserAdminAsync(currentUser);

            if (!isAdmin && currentUser?.TenantId == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedCreate",
                    "Hakem oluşturmak için bir kuruma bağlı olmanız gerekir.");

                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RefereeCreateViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await IsCurrentUserAdminAsync(currentUser);

            if (!isAdmin && currentUser?.TenantId == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedCreate",
                    "Hakem oluşturmak için bir kuruma bağlı olmanız gerekir.");

                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync("Referee"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Referee"));
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    T("Error_EmailAlreadyExists", "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var."));

                return View(model);
            }

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Institution = model.Institution,
                EmailConfirmed = true
            };

            if (!isAdmin)
            {
                user.TenantId = currentUser!.TenantId;
            }

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Referee");

                TempData["SuccessMessage"] = T(
                    "Success_RefereeCreated",
                    "Hakem başarıyla oluşturuldu.");

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = T(
                    "Error_InvalidUser",
                    "Geçersiz kullanıcı.");

                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await IsCurrentUserAdminAsync(currentUser);

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserNotFound",
                    "Kullanıcı bulunamadı.");

                return RedirectToAction(nameof(Index));
            }

            if (currentUser != null && user.Id == currentUser.Id)
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotDeleteOwnAccount",
                    "Kendi hesabınızı bu ekrandan silemezsiniz.");

                return RedirectToAction(nameof(Index));
            }

            var isReferee = await _userManager.IsInRoleAsync(user, "Referee");

            if (!isReferee)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserIsNotReferee",
                    "Bu kullanıcı hakem rolüne sahip değil.");

                return RedirectToAction(nameof(Index));
            }

            if (!isAdmin)
            {
                if (currentUser?.TenantId == null || user.TenantId != currentUser.TenantId)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_UnauthorizedDelete",
                        "Bu hakemi silme yetkiniz yok.");

                    return RedirectToAction(nameof(Index));
                }

                var targetIsAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                var targetIsOrganizator = await _userManager.IsInRoleAsync(user, "Organizator");

                if (targetIsAdmin || targetIsOrganizator)
                {
                    TempData["ErrorMessage"] = T(
                        "Error_CannotDeleteManagementUser",
                        "Yönetici rolündeki kullanıcıları silme yetkiniz yok.");

                    return RedirectToAction(nameof(Index));
                }
            }

            var deleteResult = await _userManager.DeleteAsync(user);

            if (!deleteResult.Succeeded)
            {
                TempData["ErrorMessage"] = T(
                    "Error_RefereeDeleteFailed",
                    "Hakem silinirken bir hata oluştu.");

                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = T(
                "Success_RefereeDeleted",
                "Hakem başarıyla silindi.");

            return RedirectToAction(nameof(Index));
        }
    }
}