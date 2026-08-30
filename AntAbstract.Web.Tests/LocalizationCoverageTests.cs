using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Çeviri boşlukları derlerken görünmüyor ve çoğu zaman ekranda da hata
/// vermiyor: yalnızca yanlış dilde metin çıkıyor. Bu yüzden sessizce
/// birikiyorlar.
///
/// İki ayrı durum var ve şiddetleri farklı:
///  - Görünümde <c>L("Key", "Türkçe")</c> gibi yedekli çağrı: anahtar yoksa
///    Türkçe basılır. İngilizcede yanlış dil görünür ama sayfa çalışır.
///  - Yedeksiz <c>Localizer["Key"]</c>: anahtar yoksa anahtarın kendisi
///    ekrana yazılır (örn. "RegistrationClosedShort"). Kullanıcıya bozuk
///    görünür; test bunu sıfırda tutuyor.
/// </summary>
public sealed class LocalizationCoverageTests
{
    private readonly ITestOutputHelper _output;

    public LocalizationCoverageTests(ITestOutputHelper output) => _output = output;

    private static readonly Regex Bare =
        new(@"Localizer\s*\[\s*""([^""]+)""\s*\]|Localizer\.GetString\(\s*""([^""]+)""\s*\)");

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "AntAbstract.Web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return Path.Combine(dir!.FullName, "AntAbstract.Web");
    }

    private static Dictionary<string, string> Keys(string resx)
    {
        if (!File.Exists(resx))
        {
            return new Dictionary<string, string>();
        }

        return XDocument.Load(resx).Root!
            .Elements("data")
            .Where(d => d.Attribute("name") != null)
            .GroupBy(d => d.Attribute("name")!.Value)
            .ToDictionary(g => g.Key, g => g.First().Element("value")?.Value ?? "");
    }

    /// <summary>
    /// Yedeksiz çağrının karşılığı yoksa kullanıcı anahtar adını görür.
    /// Kongre sitesinin hero bölümünde tam olarak bu olmuştu.
    /// </summary>
    [Fact]
    public void YedeksizCagrilarin_HerIkiDildeKarsiligiVar()
    {
        var root = WebRoot();
        var findings = new List<string>();

        foreach (var view in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
        {
            if (view.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                view.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var logical = Path.ChangeExtension(Path.GetRelativePath(root, view), null)!;
            var tr = Keys(Path.Combine(root, "Resources", logical + ".tr-TR.resx"));
            var en = Keys(Path.Combine(root, "Resources", logical + ".en-US.resx"));

            // Kaynak dosyası olmayan görünüm paylaşılan kaynağı kullanıyor olabilir.
            if (tr.Count == 0 && en.Count == 0)
            {
                continue;
            }

            var text = File.ReadAllText(view);

            foreach (Match m in Bare.Matches(text))
            {
                var key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;

                var missing = new List<string>();

                if (!tr.TryGetValue(key, out var trv) || string.IsNullOrWhiteSpace(trv)) missing.Add("TR");
                if (!en.TryGetValue(key, out var env) || string.IsNullOrWhiteSpace(env)) missing.Add("EN");

                if (missing.Count > 0)
                {
                    findings.Add($"{logical}: [{string.Join("+", missing)}] {key}");
                }
            }
        }

        foreach (var f in findings.Distinct().OrderBy(x => x))
        {
            _output.WriteLine(f);
        }

        Assert.True(
            findings.Count == 0,
            $"{findings.Distinct().Count()} yedeksiz çağrının karşılığı yok; " +
            "bu anahtarlar ekrana ham hâliyle basılır.");
    }
}
