using System.Text.RegularExpressions;
using Xunit;

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

    /// <summary>Dar ekranda form alanları 16px kalmalı (iOS yakınlaştırma).</summary>
    [Fact]
    public void MobilFormAlanlari_OnAltiPikselKaliyor()
    {
        Assert.Contains("font-size: 16px !important;", Css("mobile.css"), StringComparison.Ordinal);
    }
}
