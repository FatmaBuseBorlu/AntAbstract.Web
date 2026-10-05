using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Üretilmiş ama içi boş bırakılmış bir migration, modelde var olan bir
/// sütunun hiçbir veritabanında oluşmamasına yol açıyordu. Üretimde sütun
/// elle eklendiği için sorun görünmüyor, sıfırdan kurulan her veritabanında
/// bildiri gönderimi kırılıyordu:
///
///   Invalid column name 'AdminDecisionNote'.
///
/// Model anlık görüntüsü sütunu içerdiği için "migrations add" da boş dosya
/// üretiyor; yani hata kendini gizliyor. Bu test boş migration'ları görünür
/// kılıyor.
/// </summary>
public sealed class MigrationCompletenessTests
{
    private readonly ITestOutputHelper _output;

    public MigrationCompletenessTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Boş bırakılması bilinçli olan migration'lar. Yeni bir ad eklemeden
    /// önce gerçekten boş olması gerektiğinden emin olun; sebebini de yazın.
    /// </summary>
    private static readonly HashSet<string> BilerekBos = new()
    {
        // Sütunları 20260524162502_AddCertificateSignersToConferences ekliyor
        // (adı çoğul olan sonraki migration); bu dosya mükerrer kaldı.
        "20260523190805_AddCertificateSignersToConference",

        // AdminDecisionNote sütununu 20260924200000_AddMissingAdminDecisionNoteColumn
        // oluşturuyor. Bu dosya üretimde uygulanmış olarak kayıtlı olduğu için
        // içeriği sonradan doldurulamaz, bu yüzden boş bırakıldı.
        "20260615131944_AddSubmissionAdminDecisionNote",
    };

    private static string MigrationsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null &&
               !Directory.Exists(Path.Combine(dir.FullName, "AntAbstract.Infrastructure")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return Path.Combine(dir!.FullName, "AntAbstract.Infrastructure", "Migrations");
    }

    [Fact]
    public void BosMigrationYok()
    {
        var findings = new List<string>();

        foreach (var file in Directory.EnumerateFiles(MigrationsPath(), "*.cs"))
        {
            var name = Path.GetFileNameWithoutExtension(file);

            if (name.EndsWith(".Designer", StringComparison.Ordinal) ||
                name.Contains("ModelSnapshot", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            var up = Regex.Match(
                text,
                @"protected override void Up\(MigrationBuilder migrationBuilder\)\s*\{(.*?)\n        \}",
                RegexOptions.Singleline);

            if (!up.Success)
            {
                continue;
            }

            var body = Regex.Replace(up.Groups[1].Value, @"//[^\n]*", "").Trim();

            if (body.Length == 0 && !BilerekBos.Contains(name))
            {
                findings.Add(name);
            }
        }

        foreach (var f in findings)
        {
            _output.WriteLine(f);
        }

        Assert.True(
            findings.Count == 0,
            $"{findings.Count} migration'ın Up() gövdesi boş: {string.Join(", ", findings)}. " +
            "Modelde olan bir değişiklik hiçbir veritabanına uygulanmıyor demektir.");
    }

    /// <summary>
    /// Eksik sütunu ekleyen migration koşullu olmalı: üretimde sütun elle
    /// eklendiği için koşulsuz bir ALTER orada patlar.
    /// </summary>
    [Fact]
    public void EksikSutunMigrationi_KosulluCalisiyor()
    {
        var file = Path.Combine(
            MigrationsPath(), "20260924200000_AddMissingAdminDecisionNoteColumn.cs");

        Assert.True(File.Exists(file), "Eksik sütunu ekleyen migration bulunamadı.");

        var text = File.ReadAllText(file);

        Assert.Contains("COL_LENGTH", text, StringComparison.Ordinal);
        Assert.Contains("AdminDecisionNote", text, StringComparison.Ordinal);
    }
}
