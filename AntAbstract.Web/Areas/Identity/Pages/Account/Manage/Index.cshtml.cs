using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http; // Dosya yükleme için gerekli
using System.IO;

namespace AntAbstract.Web.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public IndexModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Phone]
            [Display(Name = "Telefon Numarası")]
            public string PhoneNumber { get; set; }

            [Display(Name = "Ad")]
            public string FirstName { get; set; }

            [Display(Name = "Soyad")]
            public string LastName { get; set; }

            [Display(Name = "Ünvan")]
            public string Title { get; set; }

            [Display(Name = "Üniversite/Kurum")]
            public string University { get; set; }

            [Display(Name = "Uzmanlık Alanları (Virgülle ayırın)")]
            public string ExpertiseAreas { get; set; }

            [Display(Name = "Profil Resmi")]
            public IFormFile ProfileImage { get; set; } // Resim yükleme kutusu
        }

        private async Task LoadAsync(AppUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                // Veritabanındaki bilgileri kutucuklara dolduruyoruz
                FirstName = user.FirstName,
                LastName = user.LastName,
                Title = user.Title,
                University = user.University,
                ExpertiseAreas = user.ExpertiseAreas
            };

            // Mevcut resim yolunu View'a taşıyoruz
            ViewData["CurrentProfileImage"] = user.ProfileImagePath;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            // 1. Değişen Alanları Kontrol Et ve Güncelle
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            }

            if (Input.FirstName != user.FirstName) user.FirstName = Input.FirstName;
            if (Input.LastName != user.LastName) user.LastName = Input.LastName;
            if (Input.Title != user.Title) user.Title = Input.Title;
            if (Input.University != user.University) user.University = Input.University;
            if (Input.ExpertiseAreas != user.ExpertiseAreas) user.ExpertiseAreas = Input.ExpertiseAreas;

            // 2. Profil Resmi Yükleme
            if (Input.ProfileImage != null)
            {
                var extension = Path.GetExtension(Input.ProfileImage.FileName);
                var newFileName = "profile_" + user.Id + "_" + Guid.NewGuid().ToString().Substring(0, 4) + extension;

                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "users");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, newFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await Input.ProfileImage.CopyToAsync(stream);
                }

                user.ProfileImagePath = "/uploads/users/" + newFileName;
            }

            // 3. Kaydet
            await _userManager.UpdateAsync(user);

            // 4. Oturumu Yenile (Yeni bilgilerin hemen görünmesi için)
            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = "Profiliniz başarıyla güncellendi.";
            return RedirectToPage();
        }
    }
}