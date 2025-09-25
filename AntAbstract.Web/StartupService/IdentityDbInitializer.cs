using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AntAbstract.Web.StartupServices
{
    public static class IdentityDbInitializer
    {
        // Gerekli rolleri ve varsayılan Admin kullanıcısını oluşturan statik metot.
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Gerekli Rolleri Oluşturma
            string[] roleNames = { "Admin", "Organizator", "Reviewer" };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    // Rol yoksa oluştur
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }
    }
}