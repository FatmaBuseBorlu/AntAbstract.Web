using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System; // DateTime için eklendi

namespace AntAbstract.Web.Documents
{
    // Bu sınıf, tek bir sertifikanın nasıl görüneceğini tanımlar.
    public class CertificateDocument : IDocument
    {
        // Sertifikayı oluştururken dışarıdan alacağımız dinamik bilgiler
        private readonly string _recipientName;
        private readonly string _conferenceName;

        // Yapıcı metot (Constructor): Her sertifika oluşturulduğunda bu bilgiler istenir.
        public CertificateDocument(string recipientName, string conferenceName)
        {
            _recipientName = recipientName;
            _conferenceName = conferenceName;
        }

        // PDF dosyasının başlığı gibi meta verileri belirler.
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        // Belgenin içeriğini (sayfaları, başlıkları, metinleri) oluşturduğumuz ana metot.
        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(14));

                    // Sayfanın içeriği
                    page.Content()
                        // ✅ YENİ EKLENEN SATIR: Tüm içeriği bir kutuya alıp kenarlık çiziyoruz.
                        .Border(2, Unit.Point).BorderColor(Colors.Grey.Medium)
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            // Öğeler arasında boşluk bırak
                            x.Spacing(20);

                            // 1. Satır: Ana Başlık
                            x.Item().AlignCenter().Text("Katılım Sertifikası")
                                .SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);

                            // 2. Satır: Alt Başlık
                            x.Item().AlignCenter().Text("Bu sertifika, aşağıdaki kişiye takdim edilmiştir:")
                                .FontSize(18);

                            // 3. Satır: Katılımcı Adı (Dinamik)
                            x.Item().AlignCenter().Text(_recipientName)
                                .Bold().FontSize(28).FontColor(Colors.Grey.Darken4);

                            // 4. Satır: Açıklama Metni (Dinamik)
                            x.Item().AlignCenter().Text(text =>
                            {
                                text.Span("Katılımcı, '").FontSize(18);
                                text.Span($"{_conferenceName}").SemiBold().FontSize(18);
                                text.Span("' etkinliğine başarıyla katılım sağlamıştır.").FontSize(18);
                            });

                            // 5. Satır: Tarih ve İmza Alanları
                            x.Item().PaddingTop(2, Unit.Centimetre).Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").Bold();
                                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                                });

                                row.ConstantItem(100);

                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("Kongre Başkanı").Bold();
                                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                                });
                            });
                        });
                });
        }
    }
}