using System.Text;
using System.Xml.Linq;
using AntAbstract.Web.Files;
using Microsoft.AspNetCore.Http;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Profil fotoğrafı sınırları kullanıcıyı kayıt olamaz hale getirmişti.
/// Telefon kameraları rahatlıkla 15 MB'ı aşan JPEG üretiyor.
/// </summary>
public sealed class UploadLimitTests(ITestOutputHelper output)
{
    private static readonly UploadFileValidator Validator = new();

    /// <summary>Geçerli bir JPEG üretir; imza kontrolünden geçmesi gerekir.</summary>
    private static IFormFile MakeJpeg(int sizeBytes, string name = "foto.jpg")
    {
        var bytes = new byte[sizeBytes];

        // JPEG imzası
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;

        // Dosya sonu işareti
        bytes[^2] = 0xFF; bytes[^1] = 0xD9;

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    [Theory]
    [InlineData(UploadFileProfile.RegistrationProfileImage)]
    [InlineData(UploadFileProfile.ProfileImage)]
    public async Task PhoneSizedPhoto_IsAccepted(UploadFileProfile profile)
    {
        // 12 MB — yüksek çözünürlüklü telefon fotoğrafı
        var result = await Validator.ValidateAsync(MakeJpeg(12 * 1024 * 1024), profile);

        output.WriteLine($"{profile}: 12 MB -> {(result.IsValid ? "kabul" : result.Error.ToString())}");

        Assert.True(result.IsValid, $"{profile} 12 MB'lık fotoğrafı reddetti.");
    }

    [Theory]
    [InlineData(UploadFileProfile.RegistrationProfileImage)]
    [InlineData(UploadFileProfile.ProfileImage)]
    public async Task OversizedPhoto_IsStillRejected(UploadFileProfile profile)
    {
        var result = await Validator.ValidateAsync(MakeJpeg(46 * 1024 * 1024), profile);

        output.WriteLine($"{profile}: 46 MB -> {(result.IsValid ? "kabul" : result.Error.ToString())}");

        Assert.False(result.IsValid);
        Assert.Equal(UploadValidationError.TooLarge, result.Error);
    }

    /// <summary>
    /// Tarayıcıda küçültme yapılamazsa fotoğraf olduğu gibi gelir; bu durumda
    /// bile kayıt engellenmemeli.
    /// </summary>
    [Fact]
    public async Task UnscaledLargePhoto_IsStillAccepted()
    {
        var result = await Validator.ValidateAsync(
            MakeJpeg(30 * 1024 * 1024), UploadFileProfile.RegistrationProfileImage);

        output.WriteLine($"küçültülmemiş 30 MB -> {(result.IsValid ? "kabul" : result.Error.ToString())}");

        Assert.True(result.IsValid, "Küçültülemeyen 30 MB'lık fotoğraf kaydı engelliyor.");
    }

    /// <summary>
    /// IIS sınırı uygulama sınırından düşükse büyük yüklemeler uygulamaya hiç
    /// ulaşmaz ve kullanıcı anlamsız bir sunucu hatası görür.
    /// </summary>
    [Fact]
    public async Task IisRequestLimit_IsNotBelowApplicationLimit()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "AntAbstract.Web", "web.config");

        Assert.True(File.Exists(path), $"web.config bulunamadı: {path}");

        var value = XDocument.Load(path)
            .Descendants("requestLimits")
            .Select(x => (string?)x.Attribute("maxAllowedContentLength"))
            .FirstOrDefault();

        Assert.False(string.IsNullOrWhiteSpace(value), "maxAllowedContentLength tanımlı değil.");

        var iisLimit = long.Parse(value!);
        const long kestrelLimit = 52L * 1024 * 1024;

        output.WriteLine($"IIS: {iisLimit / 1024 / 1024} MB, Kestrel: {kestrelLimit / 1024 / 1024} MB");

        Assert.True(
            iisLimit >= kestrelLimit,
            $"IIS sınırı ({iisLimit}) Kestrel sınırının ({kestrelLimit}) altında.");

        await Task.CompletedTask;
    }
}
