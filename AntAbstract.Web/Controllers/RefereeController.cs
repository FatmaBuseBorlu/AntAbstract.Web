using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using AntAbstract.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AntAbstract.Web.Controllers
{
    // SADECE ADMIN GİREBİLİR
    [Authorize(Roles = "Admin")]
    public class RefereeController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public RefereeController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // 1. HAKEM LİSTESİ
        public async Task<IActionResult> Index()
        {
            // "Referee" rolündeki kullanıcıları getir
            var referees = await _userManager.GetUsersInRoleAsync("Referee");
            return View(referees);
        }

        // 2. HAKEM EKLE (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 3. HAKEM EKLE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RefereeCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // 1. Önce "Referee" rolü var mı kontrol et, yoksa oluştur (Güvenlik)
                if (!await _roleManager.RoleExistsAsync("Referee"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Referee"));
                }

                // 2. Kullanıcı zaten var mı?
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", "Bu email adresiyle zaten bir kullanıcı kayıtlı.");
                    return View(model);
                }

                // 3. Yeni Kullanıcı Oluştur
                var user = new AppUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Institution = model.Institution,
                    EmailConfirmed = true // Admin eklediği için onaylı sayalım
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // 4. Kullanıcıya "Referee" rolünü ata
                    await _userManager.AddToRoleAsync(user, "Referee");

                    TempData["SuccessMessage"] = "Hakem başarıyla sisteme eklendi.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }

        // 4. HAKEM SİL (Opsiyonel)
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // Sadece rolü mü silsek yoksa komple kullanıcıyı mı?
                // Şimdilik komple silelim (Dikkat: Atanmış görevleri varsa hata alabilirsin, önce onları silmek gerekir)
                await _userManager.DeleteAsync(user);
                TempData["SuccessMessage"] = "Hakem silindi.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}