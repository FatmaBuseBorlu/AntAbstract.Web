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
using AntAbstract.Web.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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

        private const string ExternalLoginProviderKey = "ExternalLoginProvider";
        private const string ExternalProviderKeyKey = "ExternalProviderKey";
        private const string ExternalProviderDisplayNameKey = "ExternalProviderDisplayName";
        private const string ExternalEmailKey = "ExternalEmail";
        private const string ExternalFirstNameKey = "ExternalFirstName";
        private const string ExternalLastNameKey = "ExternalLastName";
        private const string ExternalOrcidIdKey = "ExternalOrcidId";

        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly AppDbContext _context;
        private readonly IStringLocalizer<RegisterModel> _localizer;
        private readonly IWebHostEnvironment _environment;
        private readonly IUploadFileValidator _uploadFileValidator;

        public RegisterModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<RegisterModel> logger,
            AppDbContext context,
            IStringLocalizer<RegisterModel> localizer,
            IWebHostEnvironment environment,
            IUploadFileValidator uploadFileValidator)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
            _localizer = localizer;
            _environment = environment;
            _uploadFileValidator = uploadFileValidator;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public bool IsCongressRegistrationFlow { get; set; }

        public string? CongressFlowTitle { get; set; }

        public string? CongressFlowSlug { get; set; }

        public DateTime? CongressFlowStartDate { get; set; }

        public bool IsExternalLoginFlow { get; set; }

        public string? ExternalLoginProviderName { get; set; }

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

        private class ExternalLoginState
        {
            public string Provider { get; set; } = string.Empty;

            public string ProviderKey { get; set; } = string.Empty;

            public string? DisplayName { get; set; }

            public string? Email { get; set; }

            public string? FirstName { get; set; }

            public string? LastName { get; set; }

            public string? OrcidId { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            var externalLoginState = GetExternalLoginStateFromTempData(keep: true);
            ApplyExternalLoginStateToPage(externalLoginState);

            if (externalLoginState != null)
            {
                if (string.IsNullOrWhiteSpace(Input.Email))
                {
                    Input.Email = externalLoginState.Email ?? Request.Query["email"].ToString();
                }

                if (string.IsNullOrWhiteSpace(Input.FirstName))
                {
                    Input.FirstName = externalLoginState.FirstName ?? "";
                }

                if (string.IsNullOrWhiteSpace(Input.LastName))
                {
                    Input.LastName = externalLoginState.LastName ?? "";
                }
            }
            else
            {
                var emailFromQuery = Request.Query["email"].ToString();

                if (!string.IsNullOrWhiteSpace(emailFromQuery))
                {
                    Input.Email = emailFromQuery;
                }
            }

            await LoadDropdownListsAsync();
            await LoadConferenceFlowInfoAsync(returnUrl);
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            returnUrl ??= Url.Content("~/");

            var externalLoginState = GetExternalLoginStateFromTempData(keep: true);
            ApplyExternalLoginStateToPage(externalLoginState);

            await LoadDropdownListsAsync();
            await LoadConferenceFlowInfoAsync(returnUrl);

            ValidateLegalConsents();
            ValidateOtherFields();

            if (!ModelState.IsValid)
            {
                KeepExternalLoginTempData();
                return Page();
            }

            var selectedUniversity = ResolveAcademicValue(Input.University, Input.OtherUniversity);
            var selectedFaculty = ResolveAcademicValue(Input.Faculty, Input.OtherFaculty);
            var selectedDepartment = ResolveAcademicValue(Input.Department, Input.OtherDepartment);

            var existingUser = await _userManager.FindByEmailAsync(Input.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Bu e-posta adresi ile zaten kayıtlı bir kullanıcı var.");

                KeepExternalLoginTempData();

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
                OrcidId = NormalizeOrcidId(externalLoginState?.OrcidId),

                EmailConfirmed = true
            };

            if (Input.ProfileImage == null || Input.ProfileImage.Length == 0)
            {
                ModelState.AddModelError(
                    "Input.ProfileImage",
                    GetText("ProfileImageRequired", "Profil resmi zorunludur.")
                );

                KeepExternalLoginTempData();

                return Page();
            }

            var uploadResult = await TryUploadProfileImageAsync(Input.ProfileImage);

            if (!uploadResult.Success)
            {
                ModelState.AddModelError("Input.ProfileImage", uploadResult.ErrorMessage);

                KeepExternalLoginTempData();

                return Page();
            }

            user.ProfileImagePath = uploadResult.FilePath;

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Yeni kullanıcı kaydı oluşturuldu: {Email}", user.Email);

                var externalLoginResult = await TryAddExternalLoginAsync(user, externalLoginState);

                if (!externalLoginResult.Succeeded)
                {
                    await _userManager.DeleteAsync(user);

                    foreach (var error in externalLoginResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    KeepExternalLoginTempData();

                    return Page();
                }

                await EnsureAuthorRoleAsync(user);

                var automaticRegistrationRedirectUrl =
                    await TryCreateConferencePreRegistrationFromReturnUrlAsync(user, returnUrl);

                if (externalLoginState != null)
                {
                    await _signInManager.SignInAsync(
                        user,
                        isPersistent: false,
                        authenticationMethod: externalLoginState.Provider);
                }
                else
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                }

                if (!string.IsNullOrWhiteSpace(automaticRegistrationRedirectUrl))
                {
                    return LocalRedirect(automaticRegistrationRedirectUrl);
                }

                return LocalRedirect(returnUrl);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            KeepExternalLoginTempData();

            return Page();
        }

        private ExternalLoginState? GetExternalLoginStateFromTempData(bool keep)
        {
            var provider = TempData.Peek(ExternalLoginProviderKey)?.ToString();
            var providerKey = TempData.Peek(ExternalProviderKeyKey)?.ToString();

            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerKey))
            {
                return null;
            }

            if (keep)
            {
                KeepExternalLoginTempData();
            }

            return new ExternalLoginState
            {
                Provider = provider,
                ProviderKey = providerKey,
                DisplayName = TempData.Peek(ExternalProviderDisplayNameKey)?.ToString(),
                Email = TempData.Peek(ExternalEmailKey)?.ToString(),
                FirstName = TempData.Peek(ExternalFirstNameKey)?.ToString(),
                LastName = TempData.Peek(ExternalLastNameKey)?.ToString(),
                OrcidId = TempData.Peek(ExternalOrcidIdKey)?.ToString()
            };
        }

        private void ApplyExternalLoginStateToPage(ExternalLoginState? externalLoginState)
        {
            IsExternalLoginFlow = externalLoginState != null;
            ExternalLoginProviderName = externalLoginState?.DisplayName ?? externalLoginState?.Provider;
        }

        private void KeepExternalLoginTempData()
        {
            TempData.Keep(ExternalLoginProviderKey);
            TempData.Keep(ExternalProviderKeyKey);
            TempData.Keep(ExternalProviderDisplayNameKey);
            TempData.Keep(ExternalEmailKey);
            TempData.Keep(ExternalFirstNameKey);
            TempData.Keep(ExternalLastNameKey);
            TempData.Keep(ExternalOrcidIdKey);
        }

        private async Task<IdentityResult> TryAddExternalLoginAsync(
            AppUser user,
            ExternalLoginState? externalLoginState)
        {
            if (externalLoginState == null)
            {
                return IdentityResult.Success;
            }

            var existingUserWithExternalLogin = await _userManager.FindByLoginAsync(
                externalLoginState.Provider,
                externalLoginState.ProviderKey);

            if (existingUserWithExternalLogin != null &&
                existingUserWithExternalLogin.Id != user.Id)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "ExternalLoginAlreadyLinked",
                    Description = "Bu ORCID hesabı başka bir kullanıcı hesabına bağlı görünüyor."
                });
            }

            var currentLogins = await _userManager.GetLoginsAsync(user);

            var alreadyLinked = currentLogins.Any(login =>
                login.LoginProvider == externalLoginState.Provider &&
                login.ProviderKey == externalLoginState.ProviderKey);

            if (alreadyLinked)
            {
                return IdentityResult.Success;
            }

            var loginInfo = new UserLoginInfo(
                externalLoginState.Provider,
                externalLoginState.ProviderKey,
                externalLoginState.DisplayName ?? externalLoginState.Provider);

            return await _userManager.AddLoginAsync(user, loginInfo);
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
            var isEnglish =
                CultureInfo.CurrentUICulture.Name.StartsWith(
                    "en",
                    StringComparison.OrdinalIgnoreCase);

            var list = parameters
                .Where(p => p.Group == group)
                .Select(p =>
                {
                    var displayName = isEnglish && !string.IsNullOrWhiteSpace(p.NameEn)
                        ? p.NameEn
                        : p.Name;

                    return new SelectListItem
                    {
                        Value = p.Name,
                        Text = displayName,
                        Selected = string.Equals(
                            p.Name,
                            selectedValue,
                            StringComparison.OrdinalIgnoreCase)
                    };
                })
                .ToList();

            if (includeOther)
            {
                list.Add(new SelectListItem
                {
                    Value = OtherValue,
                    Text = isEnglish ? "Other" : GetText("OtherOption", "Diğer"),
                    Selected = selectedValue == OtherValue
                });
            }

            return list;
        }
        private async Task EnsureAuthorRoleAsync(AppUser user)
        {
            const string authorRoleName = "Author";

            var roleExists = await _roleManager.RoleExistsAsync(authorRoleName);

            if (!roleExists)
            {
                await _roleManager.CreateAsync(new IdentityRole(authorRoleName));
            }

            var userIsAuthor = await _userManager.IsInRoleAsync(user, authorRoleName);

            if (!userIsAuthor)
            {
                await _userManager.AddToRoleAsync(user, authorRoleName);
            }
        }

        private async Task LoadConferenceFlowInfoAsync(string? returnUrl)
        {
            IsCongressRegistrationFlow = false;
            CongressFlowTitle = null;
            CongressFlowSlug = null;
            CongressFlowStartDate = null;

            var returnPath = NormalizeReturnUrlPath(returnUrl);

            if (string.IsNullOrWhiteSpace(returnPath))
            {
                return;
            }

            var parts = returnPath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                return;
            }

            var slug = parts[0];
            var registrationSegment = parts[1];
            var checkoutSegment = parts[2];
            var typeIdSegment = parts[3];

            var isRegistrationRoute =
                registrationSegment.Equals("registration", StringComparison.OrdinalIgnoreCase) ||
                registrationSegment.Equals("register", StringComparison.OrdinalIgnoreCase);

            var isCheckoutRoute =
                checkoutSegment.Equals("checkout", StringComparison.OrdinalIgnoreCase);

            if (!isRegistrationRoute || !isCheckoutRoute)
            {
                return;
            }

            if (!Guid.TryParse(typeIdSegment, out var registrationTypeId))
            {
                return;
            }

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .AsNoTracking()
                .FirstOrDefaultAsync(rt =>
                    rt.Id == registrationTypeId &&
                    rt.IsActive &&
                    (!rt.Deadline.HasValue || rt.Deadline.Value >= DateTime.UtcNow));

            if (ticketType == null || ticketType.Conference == null)
            {
                return;
            }

            var conference = ticketType.Conference;

            var slugMatchesConference =
                string.Equals(conference.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(conference.Tenant?.Slug, slug, StringComparison.OrdinalIgnoreCase);

            if (!slugMatchesConference)
            {
                return;
            }

            IsCongressRegistrationFlow = true;
            CongressFlowTitle = conference.Title;
            CongressFlowSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;
            CongressFlowStartDate = conference.StartDate;
        }

        private async Task<string?> TryCreateConferencePreRegistrationFromReturnUrlAsync(
            AppUser user,
            string? returnUrl)
        {
            var returnPath = NormalizeReturnUrlPath(returnUrl);

            if (string.IsNullOrWhiteSpace(returnPath))
            {
                return null;
            }

            var parts = returnPath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                return null;
            }

            var slug = parts[0];
            var registrationSegment = parts[1];
            var checkoutSegment = parts[2];
            var typeIdSegment = parts[3];

            var isRegistrationRoute =
                registrationSegment.Equals("registration", StringComparison.OrdinalIgnoreCase) ||
                registrationSegment.Equals("register", StringComparison.OrdinalIgnoreCase);

            var isCheckoutRoute =
                checkoutSegment.Equals("checkout", StringComparison.OrdinalIgnoreCase);

            if (!isRegistrationRoute || !isCheckoutRoute)
            {
                return null;
            }

            if (!Guid.TryParse(typeIdSegment, out var registrationTypeId))
            {
                return null;
            }

            var ticketType = await _context.RegistrationTypes
                .Include(rt => rt.Conference)
                    .ThenInclude(c => c.Tenant)
                .FirstOrDefaultAsync(rt =>
                    rt.Id == registrationTypeId &&
                    rt.IsActive &&
                    (!rt.Deadline.HasValue || rt.Deadline.Value >= DateTime.UtcNow));

            if (ticketType == null || ticketType.Conference == null)
            {
                return null;
            }

            var conference = ticketType.Conference;

            var slugMatchesConference =
                string.Equals(conference.Slug, slug, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(conference.Tenant?.Slug, slug, StringComparison.OrdinalIgnoreCase);

            if (!slugMatchesConference)
            {
                return null;
            }

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.AppUserId == user.Id &&
                    r.ConferenceId == conference.Id);

            if (existingRegistration == null)
            {
                var newRegistration = new Registration
                {
                    Id = Guid.NewGuid(),
                    AppUserId = user.Id,
                    ConferenceId = conference.Id,
                    RegistrationTypeId = ticketType.Id,
                    RegistrationDate = DateTime.UtcNow,
                    IsPaid = false,
                    Amount = ticketType.Price
                };

                _context.Registrations.Add(newRegistration);
                await _context.SaveChangesAsync();
            }

            var canonicalSlug = conference.Tenant?.Slug ?? conference.Slug ?? slug;

            SetSelectedConferenceSession(conference, canonicalSlug);

            TempData["SuccessMessage"] =
                "Sistem kaydınız ve kongre ön kaydınız başarıyla oluşturuldu. Şimdi bildirinizin özetini gönderebilirsiniz.";

            return $"/{canonicalSlug}/submit-abstract";
        }

        private static string NormalizeReturnUrlPath(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return string.Empty;
            }

            var decodedReturnUrl = Uri.UnescapeDataString(returnUrl);

            if (Uri.TryCreate(decodedReturnUrl, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri.AbsolutePath;
            }

            if (decodedReturnUrl.StartsWith("~/", StringComparison.Ordinal))
            {
                decodedReturnUrl = decodedReturnUrl[1..];
            }

            var queryIndex = decodedReturnUrl.IndexOf('?');

            if (queryIndex >= 0)
            {
                decodedReturnUrl = decodedReturnUrl[..queryIndex];
            }

            return decodedReturnUrl;
        }

        private void SetSelectedConferenceSession(Conference conference, string slug)
        {
            HttpContext.Session.SetString("SelectedConferenceId", conference.Id.ToString());
            HttpContext.Session.SetString("SelectedConferenceSlug", slug);
            HttpContext.Session.SetString("SelectedConferenceTitle", conference.Title ?? "");

            HttpContext.Session.SetString($"SelectedConferenceId:{conference.TenantId}", conference.Id.ToString());
            HttpContext.Session.SetString($"SelectedConferenceSlug:{conference.TenantId}", slug);
            HttpContext.Session.SetString($"SelectedConferenceTitle:{conference.TenantId}", conference.Title ?? "");
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

        private static string? NormalizeOrcidId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            var marker = "orcid.org/";
            var markerIndex = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (markerIndex >= 0)
            {
                trimmed = trimmed[(markerIndex + marker.Length)..];
            }

            return trimmed.Length > 50 ? trimmed[..50] : trimmed;
        }

        private async Task<(bool Success, string? FilePath, string ErrorMessage)> TryUploadProfileImageAsync(IFormFile file)
        {
            var validation = await _uploadFileValidator.ValidateAsync(
                file,
                UploadFileProfile.RegistrationProfileImage);

            if (!validation.IsValid)
            {
                var errorMessage = validation.Error switch
                {
                    UploadValidationError.TooLarge =>
                        "Profil resmi en fazla 20 MB olabilir.",
                    UploadValidationError.InvalidExtension =>
                        "Profil resmi yalnızca JPG, JPEG, PNG veya WEBP formatında olabilir.",
                    _ =>
                        "Profil resminin içeriği seçilen formatla eşleşmiyor."
                };

                return (
                    false,
                    null,
                    errorMessage
                );
            }

            var folderPath = string.Empty;

            try
            {
                var webRootPath = _environment.WebRootPath;

                if (string.IsNullOrWhiteSpace(webRootPath))
                {
                    webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                folderPath = Path.Combine(webRootPath, "uploads", "users");

                Directory.CreateDirectory(folderPath);

                var newFileName = _uploadFileValidator.CreateStoredFileName(
                    validation.Extension,
                    "profile");
                var physicalFilePath = Path.Combine(folderPath, newFileName);

                await using var stream = new FileStream(physicalFilePath, FileMode.Create);
                await file.CopyToAsync(stream);

                return (
                    true,
                    "/uploads/users/" + newFileName,
                    string.Empty
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Profil resmi yüklenirken hata oluştu. FolderPath: {FolderPath}, FileName: {FileName}, FileLength: {FileLength}",
                    folderPath,
                    file.FileName,
                    file.Length
                );

                return (
                    false,
                    null,
                    "Profil resmi yüklenirken bir hata oluştu. Lütfen dosya formatını kontrol ediniz veya daha küçük bir dosya deneyiniz."
                );
            }
        }
    }
}
