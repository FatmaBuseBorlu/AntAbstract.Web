using System.Text.Json;
using AntAbstract.Web.Models.WebsiteBlocks;
using Xunit;

namespace AntAbstract.Web.Tests;

/// <summary>
/// Blok içerikleri admin formunda serileştirilip site tarafında çözülüyor.
/// Bu iki uç arasındaki sözleşmenin bozulmadığını doğrular.
/// </summary>
public class StructuredBlockContentTests
{
    // Site tarafı (ConferenceHome.cshtml) bu ayarla çözüyor.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static T RoundTrip<T>(T value)
    {
        // Admin tarafı varsayılan ayarlarla serileştiriyor (camelCase değil, PascalCase).
        var json = JsonSerializer.Serialize(value);
        var back = JsonSerializer.Deserialize<T>(json, ReadOptions);

        Assert.NotNull(back);
        return back!;
    }

    [Fact]
    public void Topics_RoundTrips()
    {
        var result = RoundTrip(new TopicsBlockContent
        {
            Description = "Aşağıdaki konularda bildiri kabul edilmektedir.",
            Items =
            {
                new TopicItem { Name = "Yapay Zekâ", Description = "Makine öğrenmesi" },
                new TopicItem { Name = "Biyoteknoloji" }
            }
        });

        Assert.Equal("Aşağıdaki konularda bildiri kabul edilmektedir.", result.Description);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Yapay Zekâ", result.Items[0].Name);
        Assert.Equal("Makine öğrenmesi", result.Items[0].Description);
    }

    [Fact]
    public void ImportantDates_RoundTrips_IncludingPassedFlag()
    {
        var result = RoundTrip(new ImportantDatesBlockContent
        {
            Items =
            {
                new ImportantDateItem { Label = "Bildiri Son Gönderim", Date = "15 Ağustos 2026", IsPassed = true },
                new ImportantDateItem { Label = "Kongre", Date = "18 Eylül 2026" }
            }
        });

        Assert.Equal(2, result.Items.Count);
        Assert.True(result.Items[0].IsPassed);
        Assert.False(result.Items[1].IsPassed);
        Assert.Equal("18 Eylül 2026", result.Items[1].Date);
    }

    [Fact]
    public void Fees_RoundTrips_IncludingHighlight()
    {
        var result = RoundTrip(new FeesBlockContent
        {
            Currency = "EUR",
            Items =
            {
                new FeeItem { Name = "Öğrenci", Price = "1500", Deadline = "1 Eylül", IsHighlighted = true },
                new FeeItem { Name = "Akademisyen", Price = "2500" }
            }
        });

        Assert.Equal("EUR", result.Currency);
        Assert.True(result.Items[0].IsHighlighted);
        Assert.Equal("2500", result.Items[1].Price);
    }

    [Fact]
    public void Committees_RoundTrips_NestedMembers()
    {
        var result = RoundTrip(new CommitteesBlockContent
        {
            Groups =
            {
                new CommitteeGroup
                {
                    Name = "Bilim Kurulu",
                    Members =
                    {
                        new CommitteeMember { Title = "Prof. Dr.", FullName = "Ayşe Yılmaz", Institution = "Ege Ü." }
                    }
                }
            }
        });

        var group = Assert.Single(result.Groups);
        Assert.Equal("Bilim Kurulu", group.Name);

        var member = Assert.Single(group.Members);
        Assert.Equal("Ayşe Yılmaz", member.FullName);
        Assert.Equal("Ege Ü.", member.Institution);
    }

    [Fact]
    public void Contact_RoundTrips()
    {
        var result = RoundTrip(new ContactBlockContent
        {
            Email = "bilgi@kongre.com",
            Phone = "+90 555 000 00 00",
            Address = "Bursa",
            MapEmbedUrl = "https://www.google.com/maps/embed?pb=x"
        });

        Assert.Equal("bilgi@kongre.com", result.Email);
        Assert.Equal("https://www.google.com/maps/embed?pb=x", result.MapEmbedUrl);
    }

    [Fact]
    public void CallForPapers_RoundTrips_Guidelines()
    {
        var result = RoundTrip(new CallForPapersBlockContent
        {
            Description = "Bildirilerinizi bekliyoruz.",
            Deadline = "15 Ağustos 2026",
            Guidelines = { "En fazla 300 kelime", "Times New Roman 12 punto" }
        });

        Assert.Equal(2, result.Guidelines.Count);
        Assert.Equal("En fazla 300 kelime", result.Guidelines[0]);
    }

    [Fact]
    public void Faq_And_Sponsors_RoundTrip()
    {
        var faq = RoundTrip(new FaqBlockContent
        {
            Questions = { new FaqItem { Question = "Kayıt nasıl yapılır?", Answer = "Siteden." } }
        });

        Assert.Equal("Kayıt nasıl yapılır?", Assert.Single(faq.Questions).Question);

        var sponsors = RoundTrip(new SponsorBlockContent
        {
            Sponsors = { new SponsorItem { Name = "ACME", Tier = "Platinum", WebsiteUrl = "https://acme.com" } }
        });

        Assert.Equal("Platinum", Assert.Single(sponsors.Sponsors).Tier);
    }

    /// <summary>
    /// Mevcut bloklar "{}" ile oluşturuluyor; bu eski kayıtların çözümü patlamamalı.
    /// </summary>
    [Fact]
    public void EmptyJson_DeserializesToEmptyContent_NotNull()
    {
        var topics = JsonSerializer.Deserialize<TopicsBlockContent>("{}", ReadOptions);
        Assert.NotNull(topics);
        Assert.Empty(topics!.Items);

        var fees = JsonSerializer.Deserialize<FeesBlockContent>("{}", ReadOptions);
        Assert.NotNull(fees);
        Assert.Empty(fees!.Items);

        var committees = JsonSerializer.Deserialize<CommitteesBlockContent>("{}", ReadOptions);
        Assert.NotNull(committees);
        Assert.Empty(committees!.Groups);
    }

    /// <summary>
    /// Site tarafındaki ParseBlock bozuk JSON'da null dönüp sayfayı ayakta tutuyor.
    /// Aynı davranışın JsonException ile geldiğini sabitler.
    /// </summary>
    [Fact]
    public void MalformedJson_ThrowsJsonException_SoRendererCanCatchIt()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<TopicsBlockContent>("{ bozuk", ReadOptions));
    }

    /// <summary>
    /// JSON'da koleksiyon açıkça "null" ise deserialize onu null bırakır.
    /// Editör bu listeyi doğrudan gezdiği için controller'ın normalleştirmesi şart —
    /// bu test o varsayımı kayıt altına alır.
    /// </summary>
    [Fact]
    public void ExplicitNullCollection_DeserializesAsNull_RequiringNormalization()
    {
        var topics = JsonSerializer.Deserialize<TopicsBlockContent>(
            """{"Items":null}""", ReadOptions);

        Assert.NotNull(topics);
        Assert.Null(topics!.Items);

        var committees = JsonSerializer.Deserialize<CommitteesBlockContent>(
            """{"Groups":null}""", ReadOptions);

        Assert.NotNull(committees);
        Assert.Null(committees!.Groups);
    }
}
