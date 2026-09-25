using System.Text.RegularExpressions;
using System.Xml.Linq;
using AntAbstract.Web.Models.ViewModels.Admin.Submissions;
using System.ComponentModel.DataAnnotations;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Yazar alanlarında uzunluk sınırı tanımlı değildi. Sınırı aşan bir değer
/// doğrulamadan geçip kaydetme anında patlıyor, kullanıcıya da
/// "An error occurred while saving the entity changes" gibi hiçbir şey
/// anlatmayan bir mesaj dönüyordu. Canlıda bir yazar bu yüzden bildirisini
/// hiç gönderemedi; gerçek sebep ORCID alanına tam adresin yapıştırılmasıydı
/// (sütun 50 karakter).
///
/// Testler iki şeyi bağlıyor: sınırların veritabanı sütunlarıyla aynı kalması
/// ve sınır aşıldığında hatanın kaydetmeden önce yakalanması.
/// </summary>
public sealed class SubmissionFieldLimitTests
{
    private readonly ITestOutputHelper _output;

    public SubmissionFieldLimitTests(ITestOutputHelper output) => _output = output;

    /// <summary>Veritabanındaki sütun genişlikleri (migration'lardan).</summary>
    [Theory]
    [InlineData(nameof(SubmissionAuthorViewModel.FirstName), 100)]
    [InlineData(nameof(SubmissionAuthorViewModel.LastName), 100)]
    [InlineData(nameof(SubmissionAuthorViewModel.Institution), 200)]
    [InlineData(nameof(SubmissionAuthorViewModel.Email), 200)]
    [InlineData(nameof(SubmissionAuthorViewModel.ORCID), 50)]
    public void YazarAlanlari_VeritabaniSutunuKadarSinirli(string property, int expected)
    {
        var attribute = typeof(SubmissionAuthorViewModel)
            .GetProperty(property)!
            .GetCustomAttributes(typeof(StringLengthAttribute), false)
            .Cast<StringLengthAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);

        Assert.Equal(expected, attribute!.MaximumLength);
    }

    /// <summary>
    /// Sınırı aşan değer doğrulamada yakalanmalı. Yakalanmazsa kaydetme
    /// anında veritabanı hatası olarak dönüyor ve sebebi anlaşılmıyor.
    /// </summary>
    [Fact]
    public void UzunOrcid_DogrulamadaYakalaniyor()
    {
        var model = new SubmissionAuthorViewModel
        {
            FirstName = "Giuseppe",
            LastName = "Stracuzzi",
            Institution = "Università di Messina",
            Email = "g@example.com",
            // Kullanıcının gerçekte yaptığı: tam adresi yapıştırmak.
            ORCID = "https://orcid.org/0000-0002-1825-0097 (corresponding author profile)"
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            model, new ValidationContext(model), results, validateAllProperties: true);

        foreach (var r in results)
        {
            _output.WriteLine($"{string.Join(",", r.MemberNames)}: {r.ErrorMessage}");
        }

        Assert.False(valid, "Sınırı aşan ORCID doğrulamadan geçti; kaydetme anında patlar.");
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SubmissionAuthorViewModel.ORCID)));
    }

    [Fact]
    public void UzunKurumVeAd_DogrulamadaYakalaniyor()
    {
        var model = new SubmissionAuthorViewModel
        {
            FirstName = new string('A', 120),
            LastName = "Stracuzzi",
            Institution = new string('K', 260),
            Email = "g@example.com"
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SubmissionAuthorViewModel.FirstName)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SubmissionAuthorViewModel.Institution)));
    }

    /// <summary>Doğrulama mesajları her iki dilde tanımlı olmalı.</summary>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("en-US")]
    public void DogrulamaMesajlari_HerIkiDildeVar(string culture)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "AntAbstract.Web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        var path = Path.Combine(
            dir!.FullName, "AntAbstract.Web", "Resources", "Models", "ViewModels",
            "Admin", "Submissions", $"SubmissionAuthorViewModel.{culture}.resx");

        Assert.True(File.Exists(path), $"{culture} kaynak dosyası yok: {path}");

        var keys = XDocument.Load(path).Root!
            .Elements("data")
            .Select(d => d.Attribute("name")!.Value)
            .ToList();

        Assert.Contains("ORCID en fazla 50 karakter olabilir.", keys);
        Assert.Contains("Kurum en fazla 200 karakter olabilir.", keys);
    }
}
