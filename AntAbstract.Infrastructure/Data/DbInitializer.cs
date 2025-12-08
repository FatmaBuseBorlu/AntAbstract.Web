using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            string[] roleNames = { "Admin", "Organizator", "Reviewer", "Author" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var adminUser = new AppUser
            {
                UserName = "admin@antabstract.com",
                Email = "admin@antabstract.com",
                EmailConfirmed = true,
                FirstName = "Sistem",
                LastName = "Yöneticisi",
                City = "Ankara",
                IdentityNumber = "11111111111",
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

            if (!context.Tenants.Any())
            {
                var tenantVet = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = "3. Uluslararası Veteriner Farmakoloji Kongresi",
                    Slug = "vet2025",
                    LogoUrl = "https://placehold.co/200x200/0056b3/ffffff?text=VET+LOGO"
                };

                var tenantTech = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = "International Coding & AI Summit",
                    Slug = "techsummit",
                    LogoUrl = "https://placehold.co/200x200/ff5722/ffffff?text=AI+SUMMIT"
                };

                var tenantTourism = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = "Uluslararası Kırsal Turizm Kongresi",
                    Slug = "irtad2025",
                    LogoUrl = "https://placehold.co/200x200/28a745/ffffff?text=IRTAD"
                };

                context.Tenants.AddRange(tenantVet, tenantTech, tenantTourism);
                await context.SaveChangesAsync();


                var confVet = new Conference
                {
                    Title = "3. Uluslararası Veteriner Farmakoloji ve Toksikoloji Kongresi",
                    Description = "<p>Veteriner farmakolojisi alanındaki en güncel gelişmelerin tartışılacağı, uluslararası katılımlı bilimsel şölen.</p><ul><li>İlaç etkileşimleri</li><li>Klinik toksikoloji</li><li>Yeni tedavi yöntemleri</li></ul>",
                    StartDate = DateTime.Now.AddMonths(2),
                    EndDate = DateTime.Now.AddMonths(2).AddDays(3),
                    City = "Antalya",
                    Country = "Türkiye",
                    Venue = "Titanic Mardan Palace",
                    BannerPath = "https://placehold.co/1200x400/004085/ffffff?text=VETERINER+KONGRESI+BANNER",
                    LogoPath = tenantVet.LogoUrl,
                    Slug = tenantVet.Slug,
                    TenantId = tenantVet.Id
                };

                var confTech = new Conference
                {
                    Title = "International Summit on Artificial Intelligence 2025",
                    Description = "<p>Yapay zeka, makine öğrenmesi ve büyük veri konularında dünyanın önde gelen uzmanlarını bir araya getiriyoruz.</p><p>Geleceğin teknolojilerini bugünden keşfedin.</p>",
                    StartDate = DateTime.Now.AddMonths(5),
                    EndDate = DateTime.Now.AddMonths(5).AddDays(2),
                    City = "İstanbul",
                    Country = "Türkiye",
                    Venue = "Lütfi Kırdar Kongre Merkezi",
                    BannerPath = "https://placehold.co/1200x400/d84315/ffffff?text=AI+SUMMIT+BANNER",
                    LogoPath = tenantTech.LogoUrl,
                    Slug = tenantTech.Slug,
                    TenantId = tenantTech.Id
                };

                var confTourism = new Conference
                {
                    Title = "14. Uluslararası Kırsal Turizm ve Kalkınma Kongresi",
                    Description = "<p>Sürdürülebilir turizm ve kırsal kalkınma modellerinin inceleneceği akademik buluşma.</p>",
                    StartDate = DateTime.Now.AddMonths(1),
                    EndDate = DateTime.Now.AddMonths(1).AddDays(4),
                    City = "Nevşehir",
                    Country = "Türkiye",
                    Venue = "Kapadokya Üniversitesi",
                    BannerPath = "https://placehold.co/1200x400/1e7e34/ffffff?text=TURIZM+BANNER",
                    LogoPath = tenantTourism.LogoUrl,
                    Slug = tenantTourism.Slug,
                    TenantId = tenantTourism.Id
                };

                context.Conferences.AddRange(confVet, confTech, confTourism);
                await context.SaveChangesAsync();
            }
        }
    }
}