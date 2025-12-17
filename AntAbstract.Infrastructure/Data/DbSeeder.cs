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

            string[] roleNames = { "Admin", "Author", "Referee", "Listener" };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }


            await CreateUser(userManager, "admin@ant.com", "Admin", "User", "Admin123!", "Admin");

            await CreateUser(userManager, "hakem@ant.com", "Hakem", "Ahmet", "Hakem123!", "Referee");

            await CreateUser(userManager, "yazar@ant.com", "Yazar", "Mehmet", "Yazar123!", "Author");

            await CreateUser(userManager, "ogrenci@ant.com", "Ogrenci", "Ayse", "Ogrenci123!", "Listener");
        }

        private static async Task CreateUser(UserManager<AppUser> userManager, string email, string fName, string lName, string password, string role)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = fName,
                    LastName = lName,
                    EmailConfirmed = true
                };
                var createPowerUser = await userManager.CreateAsync(user, password);
                if (createPowerUser.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}