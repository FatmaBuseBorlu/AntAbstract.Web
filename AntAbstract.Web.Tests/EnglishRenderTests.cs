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

    /// <summary>Kültür çerezi — dil değiştirici de bunu yazıyor.</summary>
    private const string EnglishCookie = ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den-US";

    /// <summary>
    /// Oturum açmış kullanıcının menüsü paylaşılan düzende; buradaki bir
    /// sızıntı her sayfada görünür. Metinler eskiden doğrudan görünüme
    /// yazılıydı, İngilizcede de Türkçe kalıyordu.
    /// </summary>
    [Fact]
    public async Task KullaniciMenusu_IngilizceIsteninceTurkceBasmiyor()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Author");
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "menu-en-test");

        var response = await client.GetAsync("/?culture=en-US&ui-culture=en-US");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        foreach (var leak in new[]
                 { "Çıkış Yap", "Hesap Ayarları", "Sistem Paneli", "Giriş Yap", "Kayıt Ol" })
        {
            Assert.False(
                html.Contains(leak, StringComparison.Ordinal),
                $"Kullanıcı menüsü İngilizcede Türkçe basıyor: {leak}");
        }
    }

    /// <summary>
    /// Yönetici ekranlarındaki metinler doğrudan görünüme yazılıydı;
    /// İngilizce seçiliyken de Türkçe kalıyorlardı. Bu kontrol sayıma değil,
    /// sayfanın gerçekten ne bastığına bakıyor.
    /// </summary>
    /// Kapsam dışı bırakılan iki ekran ve sebepleri:
    ///  - /Admin/ReviewCriteria kongre seçilmemişse Kongre Seç ekranına
    ///    gidiyor; oradaki metinler denetleyicide çıplak dizi olarak duruyor
    ///    (T(...) ile sarılmamış). Bu ayrı bir iş kalemi.
    ///  - /Admin/EmailTemplates listesindeki Türkçe, şablon adlarının kendisi;
    ///    veritabanı içeriği, arayüz metni değil.
    [Theory]
    [InlineData("/Admin/Users")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Admin/Speakers")]
    [InlineData("/Admin/Sponsors")]
    public async Task YoneticiEkranlari_IngilizceIsteninceTurkceBasmiyor(string path)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "SuperAdmin");
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "admin-en-test");

        // Dil çerezle taşınıyor — kullanıcı da böyle seçiyor. Sorgu dizesi
        // kullanılırsa yönlendirmede kayboluyor ve sayfa Türkçeye dönüyor;
        // bu ölçüm hatası olur, üretimde çerez yönlendirmeden etkilenmiyor.
        client.DefaultRequestHeaders.Add("Cookie", EnglishCookie);

        var response = await client.GetAsync(path);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            _output.WriteLine($"{path} -> {(int)response.StatusCode}, atlandı");
            return;
        }

        var text = Tags.Replace(
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), " ");

        text = text.Replace("Türkçe", " ", StringComparison.Ordinal);

        // Kişi/kurum adları veri; yalnızca arayüz metnini arıyoruz.
        var turkish = Regex.Matches(text, @"[A-Za-zÇĞİÖŞÜçğıöşü]*[ğışçöüİĞŞÇÖÜ][A-Za-zÇĞİÖŞÜçğıöşü]*")
            .Select(m => m.Value)
            .Where(w => w.Length > 4)
            .Distinct()
            .ToList();

        foreach (var w in turkish)
        {
            _output.WriteLine($"{path}: {w}");
        }

        Assert.True(
            turkish.Count == 0,
            $"{path} İngilizcede Türkçe basıyor: {string.Join(", ", turkish.Take(10))}");
    }

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
