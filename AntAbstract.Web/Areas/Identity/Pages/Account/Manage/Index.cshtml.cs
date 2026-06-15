using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AntAbstract.Web.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private const long MaxProfileImageSize = 2 * 1024 * 1024; // 2 MB

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png"
        };

        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public IndexModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; } = string.Empty;

        public string? CurrentProfileImage { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            [Phone(ErrorMessage = "Lütfen geçerli bir telefon numarası giriniz.")]
            [Display(Name = "Telefon Numarası")]
            public string? PhoneNumber { get; set; }

            [Display(Name = "Ad")]
            [StringLength(100, ErrorMessage = "Ad alanı en fazla 100 karakter olabilir.")]
            public string? FirstName { get; set; }

            [Display(Name = "Soyad")]
            [StringLength(100, ErrorMessage = "Soyad alanı en fazla 100 karakter olabilir.")]
            public string? LastName { get; set; }

            [Display(Name = "Ünvan")]
            [StringLength(150, ErrorMessage = "Ünvan alanı en fazla 150 karakter olabilir.")]
            public string? Title { get; set; }

            [Display(Name = "Üniversite / Kurum")]
            [StringLength(250, ErrorMessage = "Üniversite / kurum alanı en fazla 250 karakter olabilir.")]
            public string? University { get; set; }

            [Display(Name = "Uzmanlık Alanları")]
            [StringLength(1000, ErrorMessage = "Uzmanlık alanları en fazla 1000 karakter olabilir.")]
            public string? ExpertiseAreas { get; set; }

            [Display(Name = "Fakülte")]
            [StringLength(250)]
            public string? Faculty { get; set; }

            [Display(Name = "Bölüm")]
            [StringLength(250)]
            public string? Department { get; set; }

            [Display(Name = "ORCID ID")]
            [StringLength(100)]
            public string? OrcidId { get; set; }

            [Display(Name = "ResearcherID")]
            [StringLength(100)]
            public string? ResearcherId { get; set; }

            [Display(Name = "Google Scholar Profil Linki")]
            [StringLength(500)]
            [Url(ErrorMessage = "Lütfen geçerli bir URL giriniz.")]
            public string? GoogleScholarLink { get; set; }

            [Display(Name = "Profil Fotoğrafı")]
            public IFormFile? ProfileImage { get; set; }
        }

        private async Task LoadAsync(AppUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName ?? string.Empty;
            CurrentProfileImage = user.ProfileImagePath;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Title = user.Title,
                University = user.University,
                ExpertiseAreas = user.ExpertiseAreas,
                Faculty = user.Faculty,
                Department = user.Department,
                OrcidId = user.OrcidId,
                ResearcherId = user.ResearcherId,
                GoogleScholarLink = user.GoogleScholarLink
            };
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

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            if (Input.PhoneNumber != phoneNumber)
            {
                await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            }

            user.FirstName = Input.FirstName?.Trim();
            user.LastName = Input.LastName?.Trim();
            user.Title = Input.Title?.Trim();
            user.University = Input.University?.Trim();
            user.ExpertiseAreas = Input.ExpertiseAreas?.Trim();
            user.Faculty = Input.Faculty?.Trim();
            user.Department = Input.Department?.Trim();
            user.OrcidId = Input.OrcidId?.Trim();
            user.ResearcherId = Input.ResearcherId?.Trim();
            user.GoogleScholarLink = Input.GoogleScholarLink?.Trim();

            if (Input.ProfileImage != null && Input.ProfileImage.Length > 0)
            {
                var extension = Path.GetExtension(Input.ProfileImage.FileName).ToLowerInvariant();

                if (!AllowedImageExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        "Input.ProfileImage",
                        "Profil fotoğrafı sadece JPG, JPEG veya PNG formatında olmalıdır.");

                    await LoadAsync(user);
                    return Page();
                }

                if (Input.ProfileImage.Length > MaxProfileImageSize)
                {
                    ModelState.AddModelError(
                        "Input.ProfileImage",
                        "Profil fotoğrafı en fazla 2 MB olabilir.");

                    await LoadAsync(user);
                    return Page();
                }

                var folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "users");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var newFileName = $"profile_{user.Id}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(folderPath, newFileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await Input.ProfileImage.CopyToAsync(stream);
                }

                if (!string.IsNullOrWhiteSpace(user.ProfileImagePath) &&
                    user.ProfileImagePath.StartsWith("/uploads/users/", StringComparison.OrdinalIgnoreCase))
                {
                    var oldFileName = Path.GetFileName(user.ProfileImagePath);
                    var oldFilePath = Path.Combine(folderPath, oldFileName);

                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                user.ProfileImagePath = "/uploads/users/" + newFileName;
            }

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                await LoadAsync(user);
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = "Profil bilgileriniz başarıyla güncellendi.";

            return RedirectToPage();
        }
    }
}