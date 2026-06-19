using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context)
        {
            string[] roleNames =
            {
                "SuperAdmin",
                "Admin",
                "Author",
                "Listener",
                "Referee"
            };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var superAdminEmail = "admin@antabstract.com.tr";

            var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);

            if (superAdmin == null)
            {
                superAdmin = new AppUser
                {
                    UserName = superAdminEmail,
                    Email = superAdminEmail,
                    EmailConfirmed = true,
                    FirstName = "Sistem",
                    LastName = "Yöneticisi",
                    City = "Ankara",
                    IdentityNumber = "11111111111",
                    Title = "Super Admin",
                    TenantId = null
                };

                var result = await userManager.CreateAsync(superAdmin, "P@ssword123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
                }

                return;
            }

            superAdmin.FirstName = "Sistem";
            superAdmin.LastName = "Yöneticisi";
            superAdmin.Title = "Super Admin";
            superAdmin.TenantId = null;
            superAdmin.EmailConfirmed = true;

            await userManager.UpdateAsync(superAdmin);

            var currentRoles = await userManager.GetRolesAsync(superAdmin);

            var rolesToRemove = currentRoles
                .Where(role => role != "SuperAdmin")
                .ToList();

            if (rolesToRemove.Any())
            {
                await userManager.RemoveFromRolesAsync(superAdmin, rolesToRemove);
            }

            if (!await userManager.IsInRoleAsync(superAdmin, "SuperAdmin"))
            {
                await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
            }
        }
    }
}