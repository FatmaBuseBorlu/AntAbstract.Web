using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Form doğrulama sınırı, yazdığı veritabanı sütunundan geniş olmamalı.
///
/// Aradaki boşluk sessiz bir tuzak: değer doğrulamadan geçiyor, kaydetme
/// anında veritabanı reddediyor ve kullanıcıya "An error occurred while
/// saving the entity changes" gibi hiçbir şey anlatmayan bir mesaj dönüyor.
/// Canlıda iki kez bu yaşandı — biri yazarın bildirisini hiç gönderemediği
/// ORCID alanı, diğeri profil formundaki ad alanı (form 100, sütun 50).
///
/// Sütun genişlikleri varlık sınıflarından okunuyor; migration'lar bunlardan
/// üretildiği için tek doğru kaynak onlar.
/// </summary>
public sealed class FormLengthMatchesDatabaseTests
{
    private readonly ITestOutputHelper _output;

    public FormLengthMatchesDatabaseTests(ITestOutputHelper output) => _output = output;

    /// <summary>Form alanı → yazdığı varlık ve özellik.</summary>
    public static TheoryData<string, string, string, string> Eslemeler() => new()
    {
        // Profil düzenleme → AppUser
        { "AntAbstract.Web.Areas.Identity.Pages.Account.Manage.IndexModel+InputModel", "FirstName", "AppUser", "FirstName" },
        { "AntAbstract.Web.Areas.Identity.Pages.Account.Manage.IndexModel+InputModel", "LastName", "AppUser", "LastName" },
        { "AntAbstract.Web.Areas.Identity.Pages.Account.Manage.IndexModel+InputModel", "Title", "AppUser", "Title" },
        { "AntAbstract.Web.Areas.Identity.Pages.Account.Manage.IndexModel+InputModel", "OrcidId", "AppUser", "OrcidId" },
        // Bildiri yazarları → SubmissionAuthor
        { "AntAbstract.Web.Models.ViewModels.Admin.Submissions.SubmissionAuthorViewModel", "FirstName", "SubmissionAuthor", "FirstName" },
        { "AntAbstract.Web.Models.ViewModels.Admin.Submissions.SubmissionAuthorViewModel", "LastName", "SubmissionAuthor", "LastName" },
        { "AntAbstract.Web.Models.ViewModels.Admin.Submissions.SubmissionAuthorViewModel", "Institution", "SubmissionAuthor", "Institution" },
        { "AntAbstract.Web.Models.ViewModels.Admin.Submissions.SubmissionAuthorViewModel", "ORCID", "SubmissionAuthor", "ORCID" },
        // Hakem ekleme → AppUser
        { "AntAbstract.Web.Models.ViewModels.Admin.Referee.RefereeCreateViewModel", "FirstName", "AppUser", "FirstName" },
        { "AntAbstract.Web.Models.ViewModels.Admin.Referee.RefereeCreateViewModel", "LastName", "AppUser", "LastName" },
    };

    private static int? LimitOf(Type type, string property)
    {
        var prop = type.GetProperty(property);

        if (prop == null)
        {
            return null;
        }

        var lengths = prop.GetCustomAttributes()
            .Select(a => a switch
            {
                StringLengthAttribute s => s.MaximumLength,
                MaxLengthAttribute m => m.Length,
                _ => (int?)null
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        return lengths.Count == 0 ? null : lengths.Min();
    }

    private static Type Resolve(string typeName)
    {
        foreach (var asm in new[]
                 {
                     typeof(AntAbstract.Domain.Entities.AppUser).Assembly,
                     Assembly.Load("AntAbstract.Web")
                 })
        {
            var t = asm.GetType(typeName)
                    ?? asm.GetTypes().FirstOrDefault(x => x.FullName == typeName || x.Name == typeName);

            if (t != null)
            {
                return t;
            }
        }

        throw new InvalidOperationException($"Tip bulunamadı: {typeName}");
    }

    [Theory]
    [MemberData(nameof(Eslemeler))]
    public void FormSiniri_VeritabaniSutunundanGenisDegil(
        string formType, string formProperty, string entityName, string entityProperty)
    {
        var entity = Resolve("AntAbstract.Domain.Entities." + entityName);

        var dbLimit = LimitOf(entity, entityProperty);

        Assert.True(
            dbLimit.HasValue,
            $"{entityName}.{entityProperty} için sütun sınırı tanımlı değil; eşleme güncellenmeli.");

        var formLimit = LimitOf(Resolve(formType), formProperty);

        _output.WriteLine($"{formType.Split('.').Last()}.{formProperty}: form={formLimit} db={dbLimit}");

        Assert.True(
            formLimit.HasValue,
            $"{formProperty} alanında uzunluk doğrulaması yok; sınırı aşan değer " +
            "kaydetme anında anlaşılmaz bir hata veriyor.");

        Assert.True(
            formLimit!.Value <= dbLimit!.Value,
            $"{formProperty}: form {formLimit} karakter kabul ediyor ama sütun {dbLimit}. " +
            "Arada kalan değerler doğrulamadan geçip kaydetmede patlar.");
    }
}
