using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Yaka Kartı ekranı her açılışında 500 veriyordu: görünüm
/// <c>@@section Styles</c> tanımlıyor ama _LayoutDashboard bu bölümü
/// basmıyordu. Razor bu durumda "sections have been defined but have not
/// been rendered" diye patlıyor — veriye bağlı değil, ekran hep ölü.
///
/// Diğer iki düzen bölümü basıyordu, bu yüzden aynı desen başka
/// ekranlarda sorunsuz çalışıyor ve gözden kaçıyordu.
/// </summary>
public sealed class LayoutSectionTests
{
    private readonly ITestOutputHelper _output;

    public LayoutSectionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EverySection_IsRenderedByItsLayout()
    {
        var root = FindWebRoot();

        var layoutSections = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(root, "_Layout*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);

            var rendered = Regex.Matches(text, @"RenderSection(?:Async)?\(\s*""([^""]+)""")
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Ayni ada sahip birden fazla duzen var (Views/Shared/_Layout ve
            // Areas/Identity/.../_Layout). Biri digerini ezmesin diye birlestiriyoruz.
            var key = Path.GetFileNameWithoutExtension(file);

            if (layoutSections.TryGetValue(key, out var existing))
            {
                existing.UnionWith(rendered);
            }
            else
            {
                layoutSections[key] = rendered;
            }
        }

        Assert.NotEmpty(layoutSections);

        var findings = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                Path.GetFileName(file).StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            var sections = Regex.Matches(text, @"@section\s+(\w+)")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            if (sections.Count == 0)
            {
                continue;
            }

            var layoutMatch = Regex.Match(text, @"Layout\s*=\s*""([^""]+)""");

            var layout = layoutMatch.Success
                ? Path.GetFileNameWithoutExtension(layoutMatch.Groups[1].Value)
                : "_ViewStart";

            // _ViewStart üzerinden gelen düzen: varsayılan _Layout.
            if (layout == "_ViewStart")
            {
                layout = "_Layout";
            }

            if (!layoutSections.TryGetValue(layout, out var rendered))
            {
                continue;
            }

            foreach (var section in sections.Where(s => !rendered.Contains(s)))
            {
                findings.Add($"{Path.GetFileName(file)}: \"@section {section}\" tanımlı ama {layout} bu bölümü basmıyor");
            }
        }

        foreach (var finding in findings)
        {
            _output.WriteLine(finding);
        }

        Assert.True(
            findings.Count == 0,
            $"{findings.Count} görünüm, düzeninin basmadığı bir bölüm tanımlıyor; sayfa her açılışta 500 verir.");
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
