using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly AppDbContext _context;

        public RegisterModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger,
            AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public SelectList UniversityList { get; set; }
        public SelectList TitleList { get; set; }
        public SelectList FacultyList { get; set; }
        public SelectList DepartmentList { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "İsim zorunludur")]
            public string FirstName { get; set; }

            [Required(ErrorMessage = "Soyisim zorunludur")]
            public string LastName { get; set; }

            [Required(ErrorMessage = "TC/Pasaport No zorunludur")]
            public string IdentityNumber { get; set; }

            [Required(ErrorMessage = "E-Posta zorunludur")]
            [EmailAddress(ErrorMessage = "Geçerli bir E-Posta giriniz")]
            public string Email { get; set; }

            [EmailAddress]
            public string? AlternativeEmail { get; set; }

            [Phone]
            public string PhoneNumber { get; set; }

            [Required(ErrorMessage = "Lütfen kurumunuzu seçiniz")]
            public string University { get; set; }

            [Required(ErrorMessage = "Lütfen ünvanınızı seçiniz")]
            public string Title { get; set; }

            [Required(ErrorMessage = "Lütfen fakültenizi seçiniz")]
            public string Faculty { get; set; }

            [Required(ErrorMessage = "Lütfen bölümünüzü seçiniz")]
            public string Department { get; set; }

            [Required(ErrorMessage = "Şifre zorunludur")]
            [StringLength(100, ErrorMessage = "{0} en az {2} karakter olmalıdır.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; }

            public IFormFile? ProfileImage { get; set; }

            [Range(typeof(bool), "true", "true", ErrorMessage = "Kullanım koşullarını kabul etmelisiniz.")]
            public bool TermsAccepted { get; set; }
        }

        private async Task LoadDropdownListsAsync()
        {
            var parameters = await _context.SystemParameters
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToListAsync();

            UniversityList = new SelectList(parameters.Where(p => p.Group == "University"), "Name", "Name");
            TitleList = new SelectList(parameters.Where(p => p.Group == "Title"), "Name", "Name");
            FacultyList = new SelectList(parameters.Where(p => p.Group == "Faculty"), "Name", "Name");
            DepartmentList = new SelectList(parameters.Where(p => p.Group == "Department"), "Name", "Name");
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            await LoadDropdownListsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
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
                    Department = Input.Department
                };

                if (Input.ProfileImage != null)
                {
                    try
                    {
                        var extension = Path.GetExtension(Input.ProfileImage.FileName);
                        var newFileName = Guid.NewGuid().ToString() + extension;
                        var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "users");
                        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                        var filePath = Path.Combine(folderPath, newFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await Input.ProfileImage.CopyToAsync(stream);
                        }
                        user.ProfileImagePath = "/uploads/users/" + newFileName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Resim yüklenirken hata oluştu: " + ex.Message);
                    }
                }

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Kullanıcı başarıyla oluşturuldu.");
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            await LoadDropdownListsAsync();
            return Page();
        }
    }
}