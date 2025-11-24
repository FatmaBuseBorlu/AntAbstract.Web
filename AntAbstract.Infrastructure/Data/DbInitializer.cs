using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Roller: Admin, Organizator, Reviewer, Author
            string[] roleNames = { "Admin", "Organizator", "Reviewer", "Author" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Admin Kullanıcısı Oluşturma
            var adminUser = new AppUser
            {
                UserName = "admin@antabstract.com",
                Email = "admin@antabstract.com",
                EmailConfirmed = true,
                FirstName = "Sistem",
                LastName = "Yöneticisi",
                City = "Istanbul",
                IdentityNumber = "99999999999",
                Title = "Prof. Dr."
            };

            if (await userManager.FindByEmailAsync(adminUser.Email) == null)
            {
                var result = await userManager.CreateAsync(adminUser, "P@ssword123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}
