using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context,
            IConfiguration configuration)
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

            var configuredEmail = configuration["BootstrapAdmin:Email"];
            var configuredPassword = configuration["BootstrapAdmin:Password"];
            var hasConfiguredEmail = HasConfiguredValue(configuredEmail);
            var hasConfiguredPassword = HasConfiguredValue(configuredPassword);

            if (hasConfiguredEmail != hasConfiguredPassword)
            {
                throw new InvalidOperationException(
                    "BootstrapAdmin:Email ve BootstrapAdmin:Password birlikte yapılandırılmalıdır.");
            }

            if (!hasConfiguredEmail)
            {
                return;
            }

            var superAdminEmail = configuredEmail!.Trim();
            var superAdminPassword = configuredPassword!.Trim();

            if (superAdminPassword.Length < 12)
            {
                throw new InvalidOperationException(
                    "BootstrapAdmin:Password en az 12 karakter olmalıdır.");
            }

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

                var result = await userManager.CreateAsync(superAdmin, superAdminPassword);

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

        private static bool HasConfiguredValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();

            return !trimmed.StartsWith("#{", StringComparison.Ordinal) &&
                   !trimmed.StartsWith("SET_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
