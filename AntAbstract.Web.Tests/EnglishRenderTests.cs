using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Çeviri boşluğu sayımı iki kez yanıltmıştı: bir kere Türkçe tarafındaki
/// eksikleri İngilizce boşluğu sandım, bir kere de resx'e hiç bakmayan bir
/// yardımcıyı boşluk saydım. Bu yüzden sayıya değil, sayfanın gerçekten ne
/// bastığına bakan bir kontrol de gerekiyor.
///
/// Sayfa İngilizce istendiğinde gövdesinde Türkçe'ye özgü harf geçmemeli.
/// Kullanıcı verisi (kongre adı, şehir) Türkçe olabilir; bu yüzden test
/// veri barındırmayan, yalnızca arayüz metni olan sayfalarda çalışıyor.
/// </summary>
public sealed class EnglishRenderTests : IClassFixture<AuthenticatedTestFactory>
{
    private readonly AuthenticatedTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public EnglishRenderTests(AuthenticatedTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private static readonly Regex Tags = new(@"(?is)<(script|style|svg).*?</\1>|<[^>]+>");

    [Theory]
    [InlineData("/proceedings")]
    [InlineData("/about")]
    [InlineData("/contact")]
    public async Task HerkeseAcikSayfalar_IngilizceIsteninceTurkceBasmiyor(string path)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = true });

        var response = await client.GetAsync($"{path}?culture=en-US&ui-culture=en-US");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var text = Tags.Replace(
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()),
            " ");

        // "Türkçe" dil değiştiricinin kendi etiketi — doğru kullanım.
        text = text.Replace("Türkçe", " ", StringComparison.Ordinal);

        var turkish = Regex.Matches(text, @"[A-Za-zÇĞİÖŞÜçğıöşü]*[ÇĞİÖŞÜçğıöş][A-Za-zÇĞİÖŞÜçğıöşü]*")
            .Select(m => m.Value)
            .Where(w => w.Length > 3)
            .Distinct()
            .ToList();

        foreach (var w in turkish)
        {
            _output.WriteLine(w);
        }

        Assert.True(
            turkish.Count == 0,
            $"{path} İngilizce istendiği hâlde Türkçe metin basıyor: {string.Join(", ", turkish.Take(12))}");
    }
}
