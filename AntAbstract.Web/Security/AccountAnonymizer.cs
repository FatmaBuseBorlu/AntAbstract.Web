using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AntAbstract.Web.Security
{
    /// <summary>
    /// Kullanıcı hesabını silmek yerine anonimleştirir.
    ///
    /// Satırı silmek iki yoldan kırılıyordu: hakem, mesaj ve katılım kayıtları
    /// kullanıcıya Restrict ile bağlı olduğu için veritabanı silmeyi reddediyordu
    /// (500), yazarların bildirileri ise Cascade ile birlikte siliniyor ve
    /// kongre programından, bildiri kitabından kayboluyordu.
    ///
    /// Burada kişisel veriler temizlenir, giriş yolları kapatılır; kongre
    /// kayıtları (bildiri, değerlendirme, ödeme) kimliksiz olarak kalır.
    /// Bildirideki yazar listesi (SubmissionAuthor) yayınlanmış künyenin
    /// parçası olduğundan korunur.
    /// </summary>
    public class AccountAnonymizer
    {
        public const string DeletedEmailDomain = "deleted.antabstract.invalid";

        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AccountAnonymizer> _logger;

        public AccountAnonymizer(
            UserManager<AppUser> userManager,
            AppDbContext context,
            IWebHostEnvironment env,
            ILogger<AccountAnonymizer> logger)
        {
            _userManager = userManager;
            _context = context;
            _env = env;
            _logger = logger;
        }

        public static bool IsAnonymized(AppUser user) =>
            user.Email?.EndsWith("@" + DeletedEmailDomain, StringComparison.OrdinalIgnoreCase) == true;

        public async Task<IdentityResult> AnonymizeAsync(AppUser user)
        {
            // Dış girişler (ORCID) ve şifre kaldırılır: hesaba bir daha girilemez.
            foreach (var login in await _userManager.GetLoginsAsync(user))
                await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);

            if (await _userManager.HasPasswordAsync(user))
                await _userManager.RemovePasswordAsync(user);

            var claims = await _userManager.GetClaimsAsync(user);
            if (claims.Count > 0)
                await _userManager.RemoveClaimsAsync(user, claims);

            await _userManager.RemoveAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey");
            await _userManager.RemoveAuthenticationTokenAsync(user, "[AspNetUserStore]", "RecoveryCodes");

            DeleteProfileImage(user.ProfileImagePath);

            var placeholder = $"deleted-{user.Id}@{DeletedEmailDomain}";

            user.UserName = placeholder;
            user.Email = placeholder;
            user.EmailConfirmed = false;
            user.PhoneNumber = null;
            user.PhoneNumberConfirmed = false;
            user.TwoFactorEnabled = false;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            user.FirstName = "Silinmiş";
            user.LastName = "Kullanıcı";
            user.DisplayName = null;
            user.IdentityNumber = null;
            user.AlternativeEmail = null;
            user.City = null;
            user.Address = null;
            user.University = null;
            user.Institution = null;
            user.Faculty = null;
            user.Department = null;
            user.Title = null;
            user.Profession = null;
            user.ExpertiseAreas = null;
            user.ProfileImagePath = null;
            user.OrcidId = null;
            user.ResearcherId = null;
            user.GoogleScholarLink = null;
            user.ReviewerUnavailableStartDate = null;
            user.ReviewerUnavailableEndDate = null;
            user.ReviewerUnavailableReason = null;
            user.ReviewerConflictInstitutions = null;
            user.ReviewerConflictPeople = null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return result;

            // Oturumları geçersiz kılar (açık başka sekme/cihaz varsa düşer).
            await _userManager.UpdateSecurityStampAsync(user);

            // Bildirimler yalnızca kullanıcıya aittir, saklanması için sebep yok.
            var notifications = await _context.Notifications
                .IgnoreQueryFilters()
                .Where(n => n.UserId == user.Id)
                .ToListAsync();
            if (notifications.Count > 0)
            {
                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Kullanıcı {UserId} hesabını sildi (anonimleştirildi).", user.Id);
            return IdentityResult.Success;
        }

        private void DeleteProfileImage(string? relativePath)
        {
            // Yalnızca kullanıcının yüklediği fotoğraf; ortak/varsayılan görseller dokunulmaz.
            if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(_env.WebRootPath) ||
                !relativePath.StartsWith("/uploads/users/", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var root = Path.GetFullPath(_env.WebRootPath);
                var full = Path.GetFullPath(Path.Combine(root, relativePath.TrimStart('/', '\\')));

                // Yalnızca wwwroot içindeki dosya silinir; ../ ile dışarı çıkılamaz.
                if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                    File.Exists(full))
                {
                    File.Delete(full);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Profil fotoğrafı silinemedi: {Path}", relativePath);
            }
        }
    }
}
