using AntAbstract.Domain.Entities;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager; 
        }

        public async Task<IActionResult> UserList()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> ManageRoles(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new AntAbstract.Web.Models.ViewModels.ManageUserRolesViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email,
                Roles = new List<AntAbstract.Web.Models.ViewModels.UserRoleViewModel>()
            };

            
            foreach (var role in _roleManager.Roles.ToList())
            {
                var userRoleViewModel = new AntAbstract.Web.Models.ViewModels.UserRoleViewModel
                {
                    RoleName = role.Name,
                    IsSelected = await _userManager.IsInRoleAsync(user, role.Name)
                };
                viewModel.Roles.Add(userRoleViewModel);
            }
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(AntAbstract.Web.Models.ViewModels.ManageUserRolesViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

     
            var existingRoles = await _userManager.GetRolesAsync(user);
            var result = await _userManager.RemoveFromRolesAsync(user, existingRoles);

            if (!result.Succeeded)
            {
                return View(model);
            }

            result = await _userManager.AddToRolesAsync(user, model.Roles.Where(r => r.IsSelected).Select(r => r.RoleName));

            if (!result.Succeeded)
            {
                return View(model);
            }

            return RedirectToAction("UserList");
        }
    }
}