using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels.Admin.Referee;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
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

            return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value)
                ? fallback
                : value.Value;
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private async Task<bool> CurrentAdminHasTenantAsync()
        {
            var currentUser = await GetCurrentUserAsync();

            return currentUser != null && currentUser.TenantId.HasValue;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Challenge();
            }

            if (!currentUser.TenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return View(new List<AppUser>());
            }

            var referees = await _userManager.GetUsersInRoleAsync("Referee");

            var filteredReferees = referees
                .Where(r =>
                    r.TenantId.HasValue &&
                    r.TenantId.Value == currentUser.TenantId.Value)
                .OrderBy(r => r.FirstName)
                .ThenBy(r => r.LastName)
                .ToList();

            return View(filteredReferees);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!await CurrentAdminHasTenantAsync())
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Hakem oluşturmak için admin hesabınıza bağlı bir kurum bulunmalıdır.");

                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RefereeCreateViewModel model)
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Challenge();
            }

            if (!currentUser.TenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Hakem oluşturmak için admin hesabınıza bağlı bir kurum bulunmalıdır.");

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

            var refereeUser = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Institution = model.Institution,
                TenantId = currentUser.TenantId.Value,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(refereeUser, model.Password);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(refereeUser, "Referee");

            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            TempData["SuccessMessage"] = T(
                "Success_RefereeCreated",
                "Hakem başarıyla oluşturuldu.");

            return RedirectToAction(nameof(Index));
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

            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Challenge();
            }

            if (!currentUser.TenantId.HasValue)
            {
                TempData["ErrorMessage"] = T(
                    "Error_AdminTenantNotFound",
                    "Admin hesabınıza bağlı kurum bulunamadı.");

                return RedirectToAction(nameof(Index));
            }

            var refereeUser = await _userManager.FindByIdAsync(id);

            if (refereeUser == null)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserNotFound",
                    "Kullanıcı bulunamadı.");

                return RedirectToAction(nameof(Index));
            }

            if (refereeUser.Id == currentUser.Id)
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotDeleteOwnAccount",
                    "Kendi hesabınızı bu ekrandan silemezsiniz.");

                return RedirectToAction(nameof(Index));
            }

            if (!refereeUser.TenantId.HasValue ||
                refereeUser.TenantId.Value != currentUser.TenantId.Value)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UnauthorizedDelete",
                    "Bu hakemi silme yetkiniz yok.");

                return RedirectToAction(nameof(Index));
            }

            var isReferee = await _userManager.IsInRoleAsync(refereeUser, "Referee");

            if (!isReferee)
            {
                TempData["ErrorMessage"] = T(
                    "Error_UserIsNotReferee",
                    "Bu kullanıcı hakem rolüne sahip değil.");

                return RedirectToAction(nameof(Index));
            }

            var targetIsSuperAdmin = await _userManager.IsInRoleAsync(refereeUser, "SuperAdmin");
            var targetIsAdmin = await _userManager.IsInRoleAsync(refereeUser, "Admin");

            if (targetIsSuperAdmin || targetIsAdmin)
            {
                TempData["ErrorMessage"] = T(
                    "Error_CannotDeleteManagementUser",
                    "Yönetici rolündeki kullanıcıları bu ekrandan silemezsiniz.");

                return RedirectToAction(nameof(Index));
            }

            var deleteResult = await _userManager.DeleteAsync(refereeUser);

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