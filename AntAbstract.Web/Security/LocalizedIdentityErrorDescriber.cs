using Microsoft.AspNetCore.Identity;
using System.Globalization;

namespace AntAbstract.Web.Security
{
    /// <summary>
    /// Identity'nin hata mesajları varsayılan olarak İngilizce: kayıt ekranında
    /// Türk kullanıcı "Passwords must have at least one uppercase ('A'-'Z')."
    /// görüyordu. Arayüz diline göre TR/EN.
    /// </summary>
    public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
    {
        private static bool En => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

        private static IdentityError E(string code, string tr, string en) =>
            new() { Code = code, Description = En ? en : tr };

        public override IdentityError PasswordTooShort(int length) =>
            E(nameof(PasswordTooShort), $"Şifre en az {length} karakter olmalıdır.", $"Password must be at least {length} characters.");

        public override IdentityError PasswordRequiresDigit() =>
            E(nameof(PasswordRequiresDigit), "Şifre en az bir rakam (0-9) içermelidir.", "Password must contain at least one digit (0-9).");

        public override IdentityError PasswordRequiresUpper() =>
            E(nameof(PasswordRequiresUpper), "Şifre en az bir büyük harf (A-Z) içermelidir.", "Password must contain at least one uppercase letter (A-Z).");

        public override IdentityError PasswordRequiresLower() =>
            E(nameof(PasswordRequiresLower), "Şifre en az bir küçük harf (a-z) içermelidir.", "Password must contain at least one lowercase letter (a-z).");

        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
            E(nameof(PasswordRequiresUniqueChars), $"Şifre en az {uniqueChars} farklı karakter içermelidir.", $"Password must use at least {uniqueChars} different characters.");

        public override IdentityError PasswordRequiresNonAlphanumeric() =>
            E(nameof(PasswordRequiresNonAlphanumeric), "Şifre en az bir özel karakter içermelidir.", "Password must contain at least one special character.");

        public override IdentityError PasswordMismatch() =>
            E(nameof(PasswordMismatch), "Şifre hatalı.", "Incorrect password.");

        public override IdentityError DuplicateEmail(string email) =>
            E(nameof(DuplicateEmail), $"{email} adresiyle kayıtlı bir hesap zaten var.", $"An account with {email} already exists.");

        public override IdentityError DuplicateUserName(string userName) =>
            E(nameof(DuplicateUserName), $"{userName} adresiyle kayıtlı bir hesap zaten var.", $"An account with {userName} already exists.");

        public override IdentityError InvalidEmail(string? email) =>
            E(nameof(InvalidEmail), $"'{email}' geçerli bir e-posta adresi değil.", $"'{email}' is not a valid email address.");

        public override IdentityError InvalidUserName(string? userName) =>
            E(nameof(InvalidUserName), $"'{userName}' geçerli bir kullanıcı adı değil.", $"'{userName}' is not a valid user name.");

        public override IdentityError InvalidToken() =>
            E(nameof(InvalidToken), "Bağlantının süresi dolmuş ya da geçersiz. Lütfen yeniden isteyin.", "The link has expired or is invalid. Please request a new one.");

        public override IdentityError DefaultError() =>
            E(nameof(DefaultError), "Beklenmeyen bir hata oluştu.", "An unexpected error occurred.");
    }
}
