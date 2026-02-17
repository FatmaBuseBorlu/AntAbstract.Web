using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndUsers(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

            string[] roleNames = { "Admin", "Author", "Referee", "Listener", "Editor", "Organizator", "Reviewer" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            await CreateOrUpdateUser(userManager,
                email: "admin@ant.com",
                firstName: "Admin",
                lastName: "User",
                password: "Admin123!",
                role: "Admin"
            );

            await CreateOrUpdateUser(userManager,
                email: "hakem@ant.com",
                firstName: "Hakem",
                lastName: "Ahmet",
                password: "Hakem123!",
                role: "Referee"
            );

            await CreateOrUpdateUser(userManager,
                email: "yazar@ant.com",
                firstName: "Yazar",
                lastName: "Mehmet",
                password: "Yazar123!",
                role: "Author"
            );

            await CreateOrUpdateUser(userManager,
                email: "ogrenci@ant.com",
                firstName: "Ogrenci",
                lastName: "Ayse",
                password: "Ogrenci123!",
                role: "Listener"
            );
        }

        private static async Task CreateOrUpdateUser(
            UserManager<AppUser> userManager,
            string email,
            string firstName,
            string lastName,
            string password,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {

                    return;
                }
            }
            else
            {
  
            }


            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
