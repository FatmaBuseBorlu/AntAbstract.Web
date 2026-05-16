using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AntAbstract.Web.StartupServices
{
    public static class IdentityDbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

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
                var roleExists = await roleManager.RoleExistsAsync(roleName);

                if (!roleExists)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }
    }
}