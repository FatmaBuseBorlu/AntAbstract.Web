using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Kongre seçme ekranındaki "Devam Et" butonu hiç aktifleşmiyordu: script
/// bloğunun içine <c>Localizer.GetString(...)</c> diye C# çağrısı yazılmıştı.
/// Tarayıcı bunu JavaScript sanıp "Localizer is not defined" fırlatıyor,
/// fonksiyon o satırda kesiliyor ve hemen altındaki butonu aktifleştiren satır
/// hiç çalışmıyordu. Aynı hata ödeme sayfasında da vardı.
///
/// Bu hata derlenirken fark edilmiyor — Razor script içeriğine karışmıyor —
/// ve yalnızca tarayıcıda ortaya çıkıyor. Test o yüzden kaynak taraması yapıyor.
/// </summary>
public sealed class RazorInScriptTests
{
    private readonly ITestOutputHelper _output;

    public RazorInScriptTests(ITestOutputHelper output) => _output = output;

    private static readonly Regex ScriptBlock =
        new(@"<script\b[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Razor'a ait isimler script içinde ancak "@" ile geçebilir; çıplak
    /// kullanım tarayıcıda ReferenceError demektir.
    /// </summary>
    private static readonly Regex ServerOnlySymbol =
        new(@"(?<!@)\b(Localizer|TempData)\s*[\.\[]");

    public static TheoryData<string> ViewFiles()
    {
        var root = FindWebProjectRoot();
        var data = new TheoryData<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            data.Add(file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ViewFiles))]
    public void ScriptBlocks_DoNotCallServerSideHelpers(string file)
    {
        var text = File.ReadAllText(file);
        var findings = new List<string>();

        foreach (Match block in ScriptBlock.Matches(text))
        {
            var body = block.Groups[1].Value;

            foreach (Match hit in ServerOnlySymbol.Matches(body))
            {
                var lineStart = body.LastIndexOf('\n', Math.Max(0, hit.Index - 1)) + 1;
                var lineEnd = body.IndexOf('\n', hit.Index);
                var line = body[lineStart..(lineEnd < 0 ? body.Length : lineEnd)].Trim();

                // Yorum satırındaki anlatımlar sayılmasın.
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                // Aynı satırda daha önce "@" varsa isim bir Razor ifadesinin
                // içindedir (örn. @Json.Serialize(Localizer["Key"].Value)) ve
                // sunucuda çözülür — doğru kullanım budur.
                if (body[lineStart..hit.Index].Contains('@'))
                {
                    continue;
                }

                findings.Add(line);
            }
        }

        foreach (var finding in findings)
        {
            _output.WriteLine($"{Path.GetFileName(file)}: {finding}");
        }

        Assert.True(
            findings.Count == 0,
            $"{Path.GetFileName(file)} içinde script bloğunda sunucu tarafı çağrısı var; " +
            "tarayıcıda ReferenceError verir. Değeri Razor ile gömün: " +
            "const x = @Json.Serialize(Localizer[\"Key\"].Value);");
    }

    private static string FindWebProjectRoot()
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
