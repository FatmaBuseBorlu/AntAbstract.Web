using AntAbstract.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AntAbstract.Web.Documents
{
    // Bu sınıf, bir grup sertifikayı tek bir PDF'te birleştirmeyi sağlar.
    public class CertificateCollectionDocument : IDocument
    {
        private readonly List<Submission> _submissions;
        private readonly string _conferenceName;

        // Yapıcı metot, sertifikası üretilecek olan özetlerin listesini alır.
        public CertificateCollectionDocument(List<Submission> submissions, string conferenceName)
        {
            _submissions = submissions;
            _conferenceName = conferenceName;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        // Toplu belgeyi oluşturan ana metot
        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                // Her sertifika için aynı sayfa yapısını kullanacağız.
                page.Size(PageSizes.A4.Landscape());
                page.Margin(2, Unit.Centimetre);

                // Ana içerik alanı
                page.Content().Column(column =>
                {
                    // Gelen listedeki her bir özet için bir döngü başlat.
                    foreach (var submission in _submissions)
                    {
                        // Her sertifika için aynı çizim mantığını kullanan bir yardımcı metot çağır.
                        // Bu, kod tekrarını önler.
                        ComposeCertificate(column, submission.Author.DisplayName, _conferenceName);

                        // Bu son sertifika değilse, bir sonraki sertifika için yeni bir sayfa başlat.
                        if (submission != _submissions.Last())
                        {
                            column.Item().PageBreak();
                        }
                    }
                });
            });
        }

        // Tek bir sertifikanın içeriğini çizen yardımcı metot
        // Bu, CertificateDocument sınıfındaki kodun aynısıdır.
        private void ComposeCertificate(ColumnDescriptor column, string recipientName, string conferenceName)
        {
            column.Item()
                .Border(2, Unit.Point).BorderColor(Colors.Grey.Medium) // Çerçeve
                .Padding(1, Unit.Centimetre)
                .Column(x =>
                {
                    x.Spacing(20);

                    x.Item().AlignCenter().Text("Katılım Sertifikası")
                        .SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);

                    x.Item().AlignCenter().Text("Bu sertifika, aşağıdaki kişiye takdim edilmiştir:")
                        .FontSize(18);

                    // Döngüden gelen yazarın adını yazdır
                    x.Item().AlignCenter().Text(recipientName)
                        .Bold().FontSize(28).FontColor(Colors.Grey.Darken4);

                    x.Item().AlignCenter().Text(text =>
                    {
                        text.Span("Katılımcı, '").FontSize(18);
                        text.Span($"{conferenceName}").SemiBold().FontSize(18);
                        text.Span("' etkinliğine başarıyla katılım sağlamıştır.").FontSize(18);
                    });

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
        }
    }
}