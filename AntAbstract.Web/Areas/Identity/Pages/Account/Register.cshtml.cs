using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;


namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly AppDbContext _context;
        private readonly IStringLocalizer<RegisterModel> _localizer;

        public RegisterModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger,
            AppDbContext context,
            IStringLocalizer<RegisterModel> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
            _localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public SelectList UniversityList { get; set; } = default!;
        public SelectList TitleList { get; set; } = default!;
        public SelectList FacultyList { get; set; } = default!;
        public SelectList DepartmentList { get; set; } = default!;

        public class InputModel
        {
            [Required(ErrorMessage = "İsim zorunludur.")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Soyisim zorunludur.")]
            public string LastName { get; set; } = string.Empty;

            [Required(ErrorMessage = "TC/Pasaport No zorunludur.")]
            public string IdentityNumber { get; set; } = string.Empty;

            [Required(ErrorMessage = "E-posta zorunludur.")]
            [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
            public string Email { get; set; } = string.Empty;

            [EmailAddress(ErrorMessage = "Geçerli bir alternatif e-posta giriniz.")]
            public string? AlternativeEmail { get; set; }

            [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
            public string? PhoneNumber { get; set; }

            [Required(ErrorMessage = "Lütfen kurumunuzu seçiniz.")]
            public string University { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lütfen ünvanınızı seçiniz.")]
            public string Title { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lütfen fakültenizi seçiniz.")]
            public string Faculty { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lütfen bölümünüzü seçiniz.")]
            public string Department { get; set; } = string.Empty;

            [Required(ErrorMessage = "Şifre zorunludur.")]
            [StringLength(100, ErrorMessage = "{0} en az {2} karakter olmalıdır.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            public IFormFile? ProfileImage { get; set; }

            public bool TermsAccepted { get; set; }

            public bool KvkkAccepted { get; set; }

            public bool MarketingConsent { get; set; }
        }

        private async Task LoadDropdownListsAsync()
        {
            var parameters = await _context.SystemParameters
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToListAsync();

            UniversityList = new SelectList(
                parameters.Where(p => p.Group == "University"),
                "Name",
                "Name"
            );

            TitleList = new SelectList(
                parameters.Where(p => p.Group == "Title"),
                "Name",
                "Name"
            );

            FacultyList = new SelectList(
                parameters.Where(p => p.Group == "Faculty"),
                "Name",
                "Name"
            );

            DepartmentList = new SelectList(
                parameters.Where(p => p.Group == "Department"),
                "Name",
                "Name"
            );
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            await LoadDropdownListsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            returnUrl ??= Url.Content("~/");

            await LoadDropdownListsAsync();

            if (!Input.TermsAccepted)
            {
                ModelState.AddModelError(
                    "Input.TermsAccepted",
                    _localizer["TermsAcceptedRequired"]);
            }

            if (!Input.KvkkAccepted)
            {
                ModelState.AddModelError(
                    "Input.KvkkAccepted",
                    _localizer["KvkkAcceptedRequired"]);
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var existingUser = await _userManager.FindByEmailAsync(Input.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Bu e-posta adresi ile zaten kayıtlı bir kullanıcı var.");

                return Page();
            }

            var user = new AppUser
            {
                UserName = Input.Email,
                Email = Input.Email,

                FirstName = Input.FirstName,
                LastName = Input.LastName,
                IdentityNumber = Input.IdentityNumber,

                AlternativeEmail = Input.AlternativeEmail,
                PhoneNumber = Input.PhoneNumber,

                University = Input.University,
                Title = Input.Title,
                Faculty = Input.Faculty,
                Department = Input.Department,

                EmailConfirmed = true
            };

            if (Input.ProfileImage != null && Input.ProfileImage.Length > 0)
            {
                var uploadResult = await TryUploadProfileImageAsync(Input.ProfileImage);

                if (!uploadResult.Success)
                {
                    ModelState.AddModelError("Input.ProfileImage", uploadResult.ErrorMessage);
                    return Page();
                }

                user.ProfileImagePath = uploadResult.FilePath;
            }

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Yeni kullanıcı kaydı oluşturuldu: {Email}", user.Email);

                if (!await _userManager.IsInRoleAsync(user, "Author"))
                {
                    await _userManager.AddToRoleAsync(user, "Author");
                }

                await _signInManager.SignInAsync(user, isPersistent: false);

                return LocalRedirect(returnUrl);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private async Task<(bool Success, string? FilePath, string ErrorMessage)> TryUploadProfileImageAsync(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return (
                    false,
                    null,
                    "Profil resmi yalnızca JPG, JPEG, PNG veya WEBP formatında olabilir."
                );
            }

            if (file.Length > 2 * 1024 * 1024)
            {
                return (
                    false,
                    null,
                    "Profil resmi en fazla 2 MB olabilir."
                );
            }

            try
            {
                var newFileName = $"{Guid.NewGuid()}{extension}";

                var folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "users"
                );

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var filePath = Path.Combine(folderPath, newFileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                return (
                    true,
                    "/uploads/users/" + newFileName,
                    string.Empty
                );
            }
            catch
            {
                return (
                    false,
                    null,
                    "Profil resmi yüklenirken bir hata oluştu."
                );
            }
        }
    }
}