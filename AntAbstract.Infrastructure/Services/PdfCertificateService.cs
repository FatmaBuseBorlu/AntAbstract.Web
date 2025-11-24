using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AntAbstract.Application.DTOs;
using AntAbstract.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO; // File.Exists için bu using eklenmeli

namespace AntAbstract.Infrastructure.Services
{
    // Bu sınıf, Application katmanındaki arayüzü uygular.
    // Görevi, PDF oluşturma işlemini başlatmaktır.
    public class PdfCertificateService : ICertificateService
    {
        public byte[] GenerateAcceptanceCertificate(CertificateDataDto data)
        {
            // QuestPDF'in topluluk lisansını kullandığımızı belirtiyoruz.
            // Bu satırı uygulamanın başlangıcında bir kez çağırmak da yeterlidir.
            QuestPDF.Settings.License = LicenseType.Community;

            var document = new CertificateDocument(data);
            return document.GeneratePdf();
        }
    }

    // Bu sınıf, PDF'in şablonunu ve içeriğini tanımlar.
    // QuestPDF'in IDocument arayüzünü uygular.
    public class CertificateDocument : IDocument
    {
        private readonly CertificateDataDto _model;

        public CertificateDocument(CertificateDataDto model)
        {
            _model = model;
        }

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    // Not: "Calibri" fontunun sunucuda yüklü olduğundan emin olun.
                    page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Calibri")); 

                    // Sayfa Üst Bilgisi (Header)
                    page.Header().Row(row =>
                    {
                        // Sol taraf: Logo ve Kongre Adı
                        row.RelativeItem().Column(col =>
                        {
                            // Logo dosyası varsa ekle
                            if (!string.IsNullOrEmpty(_model.CongressLogoPath) && File.Exists(_model.CongressLogoPath))
                                col.Item().MaxHeight(60).Image(_model.CongressLogoPath).FitArea();

                            col.Item().PaddingTop(10).Text(_model.CongressName).SemiBold().FontSize(14);
                            col.Item().Text("an Open Access Congress").FontSize(10);
                        });

                        // Sağ taraf: Rozetler/Sponsorlar (isteğe bağlı)
                        row.ConstantItem(150); // Sağda boşluk bırakmak için veya rozet eklemek için
                    });

                    // Sayfa İçeriği (Content)
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Spacing(25); // Öğeler arasına dikey boşluk ekle

                        // Defne Tacı görseli
                        if (!string.IsNullOrEmpty(_model.LaurelWreathImagePath) && File.Exists(_model.LaurelWreathImagePath))
                            col.Item().AlignCenter().MaxHeight(80).Image(_model.LaurelWreathImagePath).FitArea();

                        col.Item().AlignCenter().Text("CERTIFICATE OF ACCEPTANCE").Bold().FontSize(26).LetterSpacing(2);

                        col.Item().PaddingTop(1, Unit.Centimetre).Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(14));
                            text.Span("The certificate of acceptance for the manuscript (");
                            text.Span($"{_model.SubmissionUniqueId}").Bold();
                            text.Span(") titled:");
                        });

                        col.Item().AlignCenter().Text($"\"{_model.SubmissionTitle}\"").FontSize(18).Italic().FontColor(Colors.Grey.Darken2);

                        col.Item().Text("Authored by:").FontSize(14);
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Text(string.Join("; ", _model.Authors)).FontSize(12);

                        col.Item().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(14));
                            text.Span("was accepted in ");
                            text.Span($"{_model.CongressName} {_model.CongressIdentifier}").Bold();
                            text.Span($" on {_model.AcceptanceDate:dd MMMM yyyy}.");
                        });
                    });

                    // Sayfa Alt Bilgisi (Footer)
                    page.Footer().Row(row =>
                    {
                        // Sol alt: Konum ve tarih
                        row.RelativeItem().Column(col => {
                            col.Item().Text($"{_model.CongressLocation}, {_model.AcceptanceDate:MMMM yyyy}");
                        });

                        // Sağ alt: İmza ve Unvan
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            if (!string.IsNullOrEmpty(_model.SignatureImagePath) && File.Exists(_model.SignatureImagePath))
                                col.Item().MaxHeight(50).Image(_model.SignatureImagePath).FitArea();

                            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                            col.Item().Text(_model.SignatoryName).Bold();
                            col.Item().Text(_model.SignatoryTitle);
                        });
                    });
                });
        }
    }
}