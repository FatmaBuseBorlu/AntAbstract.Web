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

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "Admin");

            var referees = await _userManager.GetUsersInRoleAsync("Referee");

            if (!isAdmin && currentUser?.TenantId != null)
            {
                referees = referees.Where(r => r.TenantId == currentUser.TenantId).ToList();
            }
            else if (!isAdmin && currentUser?.TenantId == null)
            {
                referees = new List<AppUser>();
            }

            return View(referees);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RefereeCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (!await _roleManager.RoleExistsAsync("Referee"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Referee"));
                }

                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", _localizer["Error_EmailAlreadyExists"]);
                    return View(model);
                }

                var currentUser = await _userManager.GetUserAsync(User);
                var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "Admin");

                var user = new AppUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Institution = model.Institution,
                    EmailConfirmed = true
                };

                if (!isAdmin && currentUser?.TenantId != null)
                {
                    user.TenantId = currentUser.TenantId;
                }

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Referee");

                    TempData["SuccessMessage"] = _localizer["Success_RefereeCreated"];
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "Admin");

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                if (!isAdmin && user.TenantId != currentUser?.TenantId)
                {
                    TempData["ErrorMessage"] = _localizer["Error_UnauthorizedDelete"];
                    return RedirectToAction(nameof(Index));
                }

                await _userManager.DeleteAsync(user);
                TempData["SuccessMessage"] = _localizer["Success_RefereeDeleted"];
            }

            return RedirectToAction(nameof(Index));
        }
    }
}