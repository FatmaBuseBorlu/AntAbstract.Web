using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private const string OtherValue = "__OTHER__";

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

        public List<SelectListItem> UniversityList { get; set; } = new();
        public List<SelectListItem> TitleList { get; set; } = new();
        public List<SelectListItem> FacultyList { get; set; } = new();
        public List<SelectListItem> DepartmentList { get; set; } = new();

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

            public string? OtherUniversity { get; set; }

            [Required(ErrorMessage = "Lütfen ünvanınızı seçiniz.")]
            public string Title { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lütfen fakültenizi seçiniz.")]
            public string Faculty { get; set; } = string.Empty;

            public string? OtherFaculty { get; set; }

            [Required(ErrorMessage = "Lütfen bölümünüzü seçiniz.")]
            public string Department { get; set; } = string.Empty;

            public string? OtherDepartment { get; set; }

            [Required(ErrorMessage = "Şifre zorunludur.")]
            [StringLength(100, ErrorMessage = "{0} en az {2} karakter olmalıdır.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Profil resmi zorunludur.")]
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

            UniversityList = BuildParameterList(
                parameters,
                "University",
                Input.University,
                includeOther: true
            );

            TitleList = BuildParameterList(
                parameters,
                "Title",
                Input.Title,
                includeOther: false
            );

            FacultyList = BuildParameterList(
                parameters,
                "Faculty",
                Input.Faculty,
                includeOther: true
            );

            DepartmentList = BuildParameterList(
                parameters,
                "Department",
                Input.Department,
                includeOther: true
            );
        }

        private List<SelectListItem> BuildParameterList(
            List<SystemParameter> parameters,
            string group,
            string? selectedValue,
            bool includeOther)
        {
            var list = parameters
                .Where(p => p.Group == group)
                .Select(p => new SelectListItem
                {
                    Value = p.Name,
                    Text = p.Name,
                    Selected = string.Equals(p.Name, selectedValue, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            if (includeOther)
            {
                list.Add(new SelectListItem
                {
                    Value = OtherValue,
                    Text = GetText("OtherOption", "Diğer / Other"),
                    Selected = selectedValue == OtherValue
                });
            }

            return list;
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

            ValidateLegalConsents();
            ValidateOtherFields();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var selectedUniversity = ResolveAcademicValue(Input.University, Input.OtherUniversity);
            var selectedFaculty = ResolveAcademicValue(Input.Faculty, Input.OtherFaculty);
            var selectedDepartment = ResolveAcademicValue(Input.Department, Input.OtherDepartment);

            await EnsureAcademicValuesExistAsync(
                selectedUniversity,
                selectedFaculty,
                selectedDepartment
            );

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
                UserName = Input.Email.Trim(),
                Email = Input.Email.Trim(),

                FirstName = NormalizeTitleCase(Input.FirstName),
                LastName = NormalizeTitleCase(Input.LastName),
                IdentityNumber = Input.IdentityNumber.Trim(),

                AlternativeEmail = string.IsNullOrWhiteSpace(Input.AlternativeEmail)
                    ? null
                    : Input.AlternativeEmail.Trim(),

                PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber)
                    ? null
                    : Input.PhoneNumber.Trim(),

                University = selectedUniversity,
                Title = NormalizeTitleCase(Input.Title),
                Faculty = selectedFaculty,
                Department = selectedDepartment,

                EmailConfirmed = true
            };

            if (Input.ProfileImage == null || Input.ProfileImage.Length == 0)
            {
                ModelState.AddModelError(
                    "Input.ProfileImage",
                    GetText("ProfileImageRequired", "Profil resmi zorunludur.")
                );

                return Page();
            }

            var uploadResult = await TryUploadProfileImageAsync(Input.ProfileImage);

            if (!uploadResult.Success)
            {
                ModelState.AddModelError("Input.ProfileImage", uploadResult.ErrorMessage);
                return Page();
            }

            user.ProfileImagePath = uploadResult.FilePath;

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

        private void ValidateLegalConsents()
        {
            if (!Input.TermsAccepted)
            {
                ModelState.AddModelError(
                    "Input.TermsAccepted",
                    GetText("TermsAcceptedRequired", "Kullanım Şartları’nı kabul etmelisiniz."));
            }

            if (!Input.KvkkAccepted)
            {
                ModelState.AddModelError(
                    "Input.KvkkAccepted",
                    GetText("KvkkAcceptedRequired", "KVKK Aydınlatma Metni’ni okuduğunuzu onaylamalısınız."));
            }
        }

        private void ValidateOtherFields()
        {
            if (Input.University == OtherValue && string.IsNullOrWhiteSpace(Input.OtherUniversity))
            {
                ModelState.AddModelError(
                    "Input.OtherUniversity",
                    GetText("OtherUniversityRequired", "Lütfen üniversite / kurum adını yazınız."));
            }

            if (Input.Faculty == OtherValue && string.IsNullOrWhiteSpace(Input.OtherFaculty))
            {
                ModelState.AddModelError(
                    "Input.OtherFaculty",
                    GetText("OtherFacultyRequired", "Lütfen fakülte adını yazınız."));
            }

            if (Input.Department == OtherValue && string.IsNullOrWhiteSpace(Input.OtherDepartment))
            {
                ModelState.AddModelError(
                    "Input.OtherDepartment",
                    GetText("OtherDepartmentRequired", "Lütfen bölüm adını yazınız."));
            }
        }

        private string ResolveAcademicValue(string selectedValue, string? otherValue)
        {
            if (selectedValue == OtherValue)
            {
                return NormalizeTitleCase(otherValue);
            }

            return NormalizeTitleCase(selectedValue);
        }

        private async Task EnsureAcademicValuesExistAsync(
            string university,
            string faculty,
            string department)
        {
            var hasChanges = false;

            if (!string.IsNullOrWhiteSpace(university))
            {
                hasChanges |= await EnsureSystemParameterExistsAsync("University", university);
            }

            if (!string.IsNullOrWhiteSpace(faculty))
            {
                hasChanges |= await EnsureSystemParameterExistsAsync("Faculty", faculty);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                hasChanges |= await EnsureSystemParameterExistsAsync("Department", department);
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task<bool> EnsureSystemParameterExistsAsync(string group, string name)
        {
            var normalizedName = NormalizeTitleCase(name);

            var exists = await _context.SystemParameters
                .AnyAsync(x =>
                    x.Group == group &&
                    x.Name.ToLower() == normalizedName.ToLower());

            if (exists)
            {
                return false;
            }

            var maxOrder = await _context.SystemParameters
                .Where(x => x.Group == group)
                .MaxAsync(x => (int?)x.Order) ?? 0;

            _context.SystemParameters.Add(new SystemParameter
            {
                Group = group,
                Name = normalizedName,
                IsActive = true,
                Order = maxOrder + 1
            });

            return true;
        }

        private static string NormalizeTitleCase(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var culture = new CultureInfo("tr-TR");

            var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");

            return culture.TextInfo.ToTitleCase(cleaned.ToLower(culture));
        }

        private string GetText(string key, string fallback)
        {
            var value = _localizer[key];

            return value.ResourceNotFound
                ? fallback
                : value.Value;
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