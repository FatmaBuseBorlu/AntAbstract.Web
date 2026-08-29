using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Telefonda tablo eylemlerine erişim.
///
/// Bildiriler listesindeki tablo 900px'e kadar genişliyor; kap yatay
/// kaydırılabilir olsa da işlem sütunu ekranın ~800px sağına düşüyordu ve
/// satır sonundaki üç nokta menüsüne hiç ulaşılamıyordu (Android'de bildirildi).
///
/// Ölçüm testi burada yapılamıyor (Razor'ı tarayıcıda çalıştırmak gerekir),
/// bu yüzden kaynakta düzeltmenin yerinde durduğu doğrulanıyor: geniş bir
/// tablonun işlem sütunu sağa yapışık olmalı.
/// </summary>
public sealed class MobileActionReachabilityTests
{
    private readonly ITestOutputHelper _output;

    public MobileActionReachabilityTests(ITestOutputHelper output) => _output = output;

    private static string ViewPath =>
        Path.Combine(FindWebRoot(), "Areas", "Admin", "Views", "Submissions", "Index.cshtml");

    [Fact]
    public void BildirilerTablosu_IslemSutunu_SagaYapisik()
    {
        var css = File.ReadAllText(ViewPath);

        // Sütun gerçekten işaretlenmiş mi.
        Assert.Contains("th class=\"text-end actions-cell\"", css, StringComparison.Ordinal);
        Assert.Contains("td class=\"text-end actions-cell\"", css, StringComparison.Ordinal);

        // Yapışkan konumlandırma tanımlı mı.
        var rule = Regex.Match(
            css,
            @"\.submission-table\s+th\.actions-cell,\s*\.submission-table\s+td\.actions-cell\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);

        Assert.True(rule.Success, "İşlem sütunu için yapışkan kural bulunamadı.");

        var body = rule.Groups["body"].Value;

        _output.WriteLine(body.Trim());

        Assert.Contains("position: sticky", body, StringComparison.Ordinal);
        Assert.Contains("right: 0", body, StringComparison.Ordinal);

        // Yapışkan hücrenin arkası saydam kalırsa altındaki satır görünür.
        Assert.Contains("background:", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kaydırma kabı olmayan geniş tablo, sütunları ekran dışına taşırır.
    /// Daha önce iki ekranda yaşandı; yenisi eklenmesin.
    /// </summary>
    [Theory]
    [InlineData("Areas/Admin/Views/Submissions/Index.cshtml", "submission-table")]
    public void GenisTablo_KaydirmaKabindaDuruyor(string relativePath, string tableClass)
    {
        var text = File.ReadAllText(Path.Combine(FindWebRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var tableIndex = text.IndexOf($"<table class=\"{tableClass}\"", StringComparison.Ordinal);

        Assert.True(tableIndex > 0, $"{tableClass} tablosu bulunamadı.");

        // Tablodan hemen önceki 200 karakterde kaydırma kabı olmalı.
        var before = text[Math.Max(0, tableIndex - 200)..tableIndex];

        Assert.Contains("table-wrap", before, StringComparison.Ordinal);
    }

    private static string FindWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "AntAbstract.Web")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, "AntAbstract.Web klasörü bulunamadı.");

        return Path.Combine(dir!.FullName, "AntAbstract.Web");
    }
}
