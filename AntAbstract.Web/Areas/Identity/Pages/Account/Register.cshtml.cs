using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using AntAbstract.Domain.Entities; 
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http; 

namespace AntAbstract.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
    

        public RegisterModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "İsim zorunludur")]
            [Display(Name = "İsim")]
            public string FirstName { get; set; }

            [Required(ErrorMessage = "Soyisim zorunludur")]
            [Display(Name = "Soyisim")]
            public string LastName { get; set; }

            [Required(ErrorMessage = "TC/Pasaport No zorunludur")]
            [Display(Name = "TC No")]
            public string IdentityNumber { get; set; }

            [Required(ErrorMessage = "E-Posta zorunludur")]
            [EmailAddress(ErrorMessage = "Geçerli bir E-Posta giriniz")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [EmailAddress]
            [Display(Name = "Alternatif E-Posta")]
            public string? AlternativeEmail { get; set; }

            [Phone]
            [Display(Name = "Cep Telefonu")]
            public string PhoneNumber { get; set; }

            [Display(Name = "Üniversite")]
            public string University { get; set; }

            [Display(Name = "Ünvan")]
            public string Title { get; set; }

            [Display(Name = "Meslek/Uzmanlık")]
            public string Profession { get; set; }

            [Display(Name = "Şehir")]
            public string City { get; set; }

            [Display(Name = "Adres")]
            public string Address { get; set; }

            [Required(ErrorMessage = "Şifre zorunludur")]
            [StringLength(100, ErrorMessage = "{0} en az {2} karakter olmalıdır.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Şifre")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Şifre Tekrar")]
            [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Profil Resmi")]
            public IFormFile? ProfileImage { get; set; }

            public bool TermsAccepted { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
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
                    Profession = Input.Profession,
                    City = Input.City,
                    Address = Input.Address
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
                    _logger.LogInformation("User created a new account with password.");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }
    }
}