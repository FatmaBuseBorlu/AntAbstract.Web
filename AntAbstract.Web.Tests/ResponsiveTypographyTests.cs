using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Arayüzün tamamı rem ile ölçülüyor ama html için hiç font-size tanımlı
/// değildi; her şey sabit 16px'e bağlıydı ve dar pencerede yazılar olduğundan
/// büyük duruyordu. Kök boyut artık ekran genişliğiyle ölçekleniyor.
///
/// Bu testler kuralın kazara silinmesini ya da mobile.css'in ölçülerek
/// ayarlanmış boyutlarının bozulmasını yakalar.
/// </summary>
public sealed class ResponsiveTypographyTests
{
    private readonly ITestOutputHelper _output;

    public ResponsiveTypographyTests(ITestOutputHelper output) => _output = output;

    private static string Css(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "AntAbstract.Web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return File.ReadAllText(
            Path.Combine(dir!.FullName, "AntAbstract.Web", "wwwroot", "css", name));
    }

    [Fact]
    public void KokYaziBoyutu_EkranGenisligiyleOlcekleniyor()
    {
        var css = Css("site.css");

        Assert.Matches(new Regex(@"html\s*\{[^}]*font-size:\s*clamp\(", RegexOptions.Singleline), css);
    }

    /// <summary>
    /// Kural yalnızca masaüstünde çalışmalı. Dar ekranların boyutları
    /// ölçülerek ayarlanmıştı; oraya sızarsa iOS'ta form yakınlaştırması
    /// ve küçük dokunma hedefleri geri gelir.
    /// </summary>
    [Fact]
    public void OlcekleyenKural_YalnizcaMasaustunde()
    {
        var css = Css("site.css");

        var match = Regex.Match(
            css,
            @"@media\s*\(min-width:\s*992px\)\s*\{\s*html\s*\{[^}]*clamp\(",
            RegexOptions.Singleline);

        Assert.True(match.Success, "Kök ölçekleme 992px medya sorgusunun içinde olmalı.");
    }

    /// <summary>Alt sınır 14px'in altına inmemeli — gövde metni okunmaz olur.</summary>
    [Fact]
    public void AltSinir_OnDortPikselinAltinaInmiyor()
    {
        var css = Css("site.css");

        var min = Regex.Match(css, @"font-size:\s*clamp\(\s*([0-9.]+)rem").Groups[1].Value;

        Assert.True(
            double.Parse(min, System.Globalization.CultureInfo.InvariantCulture) * 16 >= 14,
            $"Alt sınır {min}rem — 14px'in altında.");
    }

    /// <summary>Ölçek değişkenleri tek yerde tanımlı olmalı.</summary>
    [Theory]
    [InlineData("--fs-hero")]
    [InlineData("--fs-section")]
    [InlineData("--fs-card-title")]
    public void TipografiOlcegi_TekYerdeTanimli(string token)
    {
        Assert.Contains(token + ":", Css("site.css"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Başlıklar ölçeğe bağlı kalmalı. Sayfanın kendi içinde büyük bir
    /// clamp tanımlanırsa o sayfa ölçekten kopar ve eskisi gibi "her sayfa
    /// başka telden çalar" duruma dönülür.
    /// </summary>
    [Fact]
    public void Gorunumler_KendiBuyukBaslikOlceginiTanimlamiyor()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "AntAbstract.Web")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        var web = Path.Combine(root!.FullName, "AntAbstract.Web");
        var big = new Regex(@"font-size:\s*clamp\(\s*([2-9](?:\.\d+)?)rem");
        var findings = new List<string>();

        foreach (var view in Directory.EnumerateFiles(web, "*.cshtml", SearchOption.AllDirectories))
        {
            if (view.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                view.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            foreach (Match m in big.Matches(File.ReadAllText(view)))
            {
                findings.Add($"{Path.GetFileName(view)}: {m.Value}");
            }
        }

        foreach (var f in findings)
        {
            _output.WriteLine(f);
        }

        Assert.True(
            findings.Count == 0,
            "Sayfa içinde 2rem ve üzeri başlık ölçüsü tanımlanmış; " +
            "var(--fs-hero) veya var(--fs-section) kullanılmalı.");
    }

    /// <summary>Dar ekranda form alanları 16px kalmalı (iOS yakınlaştırma).</summary>
    [Fact]
    public void MobilFormAlanlari_OnAltiPikselKaliyor()
    {
        Assert.Contains("font-size: 16px !important;", Css("mobile.css"), StringComparison.Ordinal);
    }
}
