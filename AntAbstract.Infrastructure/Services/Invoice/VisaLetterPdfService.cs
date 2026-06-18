using AntAbstract.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;

namespace AntAbstract.Infrastructure.Services.Invoice
{
    public class VisaLetterPdfService : IVisaLetterPdfService
    {
        private const string Navy    = "#1a2d5a";
        private const string Accent  = "#2563eb";
        private const string LightBg = "#f4f6fa";
        private const string TextDark = "#1e1e2e";
        private const string TextMid  = "#555577";
        private const string Border   = "#dee2e6";

        public byte[] GenerateVisaLetter(Registration registration)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var conf = registration.Conference;
            var user = registration.AppUser;

            var fullName = string.Join(" ",
                new[] { user?.Title, user?.FirstName, user?.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            var institution = !string.IsNullOrWhiteSpace(user?.Institution)
                ? user.Institution
                : user?.University ?? "";

            var confLocation = BuildLocation(conf);
            var dateRange = $"{conf?.StartDate:dd MMMM yyyy} – {conf?.EndDate:dd MMMM yyyy}";
            var letterDate = DateTime.Now.ToString("dd MMMM yyyy");
            var refNo = $"VISA-{registration.Id.ToString("N").Substring(0, 8).ToUpper()}";

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(56);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(11).FontColor(TextDark));

                    // Header: logo alanı + kongre adı
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(conf?.Title ?? "")
                                    .FontSize(16).Bold().FontColor(Navy);
                                if (!string.IsNullOrWhiteSpace(confLocation))
                                    left.Item().Text(confLocation)
                                        .FontSize(10).FontColor(TextMid);
                                left.Item().Text(dateRange)
                                    .FontSize(10).FontColor(TextMid);
                            });

                            row.ConstantItem(120).AlignRight().Column(right =>
                            {
                                right.Item().AlignRight().Text("VİZE DESTEK MEKTUBU")
                                    .FontSize(10).Bold().FontColor(Accent);
                                right.Item().AlignRight().Text($"Ref: {refNo}")
                                    .FontSize(9).FontColor(TextMid);
                                right.Item().AlignRight().Text(letterDate)
                                    .FontSize(9).FontColor(TextMid);
                            });
                        });

                        col.Item().Height(8);
                        col.Item().BorderBottom(2).BorderColor(Navy);
                        col.Item().Height(16);
                    });

                    page.Content().Column(col =>
                    {
                        // Hitap
                        col.Item().Text("Sayın İlgili Makam,").Bold().FontSize(12);
                        col.Item().Height(12);

                        // Giriş paragrafı
                        col.Item().Text(txt =>
                        {
                            txt.DefaultTextStyle(s => s.LineHeight(1.6f));
                            txt.Span("Bu mektup, ");
                            txt.Span(fullName).Bold();
                            txt.Span($" adlı katılımcının ");
                            txt.Span(conf?.Title ?? "").Bold();
                            txt.Span($" kongresine kayıtlı bir katılımcı olduğunu onaylamak amacıyla düzenlenmiştir.");
                        });

                        col.Item().Height(12);

                        // Kongre bilgileri kutusu
                        col.Item().Background(LightBg).Padding(14).Column(box =>
                        {
                            box.Item().Text("KONGRE BİLGİLERİ").FontSize(9).Bold().FontColor(TextMid);
                            box.Item().Height(6);

                            InfoRow(box, "Kongre Adı",  conf?.Title ?? "—");
                            InfoRow(box, "Tarih",       dateRange);
                            InfoRow(box, "Yer",         string.IsNullOrWhiteSpace(confLocation) ? "—" : confLocation);
                        });

                        col.Item().Height(12);

                        // Katılımcı bilgileri kutusu
                        col.Item().Background(LightBg).Padding(14).Column(box =>
                        {
                            box.Item().Text("KATILIMCI BİLGİLERİ").FontSize(9).Bold().FontColor(TextMid);
                            box.Item().Height(6);

                            InfoRow(box, "Ad Soyad", fullName);

                            if (!string.IsNullOrWhiteSpace(user?.Email))
                                InfoRow(box, "E-posta", user.Email);

                            if (!string.IsNullOrWhiteSpace(institution))
                                InfoRow(box, "Kurum", institution);

                            if (!string.IsNullOrWhiteSpace(user?.IdentityNumber))
                                InfoRow(box, "Kimlik / Pasaport No", user.IdentityNumber);

                            InfoRow(box, "Kayıt Durumu",
                                registration.IsPaid ? "Ödeme Tamamlandı — Kayıt Onaylı" : "Kayıt Yapıldı");
                        });

                        col.Item().Height(16);

                        // Ana paragraf
                        col.Item().Text(txt =>
                        {
                            txt.DefaultTextStyle(s => s.LineHeight(1.6f));
                            txt.Span("Kongremize katılmak amacıyla seyahat edecek olan ");
                            txt.Span(fullName).Bold();
                            txt.Span(" için gerekli vize işlemlerinin olumlu değerlendirilmesini saygıyla talep ederiz.");
                        });

                        col.Item().Height(8);

                        col.Item().Text(
                            "Bu mektup yalnızca kongre katılımını desteklemek amacıyla düzenlenmiş olup başka herhangi bir amaçla kullanılamaz.")
                            .FontSize(9).FontColor(TextMid).Italic().LineHeight(1.5f);

                        col.Item().Height(32);

                        // İmza alanı
                        if (!string.IsNullOrWhiteSpace(conf?.CertificateFirstSignerName))
                        {
                            col.Item().Column(sig =>
                            {
                                sig.Item().BorderBottom(1).BorderColor(Border).Width(200).PaddingBottom(6);
                                sig.Item().Height(4);
                                sig.Item().Text(conf.CertificateFirstSignerName).Bold().FontSize(11);
                                if (!string.IsNullOrWhiteSpace(conf.CertificateFirstSignerTitle))
                                    sig.Item().Text(conf.CertificateFirstSignerTitle).FontSize(10).FontColor(TextMid);
                                sig.Item().Text(conf.Title ?? "").FontSize(10).FontColor(TextMid);
                            });
                        }
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().BorderTop(1).BorderColor(Border).PaddingTop(6);
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(
                                $"Bu mektup {letterDate} tarihinde elektronik olarak düzenlenmiştir. Ref: {refNo}")
                                .FontSize(8).FontColor(TextMid);
                            row.ConstantItem(60).AlignRight()
                                .Text(x =>
                                {
                                    x.Span("Sayfa ").FontSize(8).FontColor(TextMid);
                                    x.CurrentPageNumber().FontSize(8).FontColor(TextMid);
                                    x.Span("/").FontSize(8).FontColor(TextMid);
                                    x.TotalPages().FontSize(8).FontColor(TextMid);
                                });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static void InfoRow(ColumnDescriptor col, string label, string value)
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(160).Text(label + ":").FontSize(10).Bold().FontColor(TextMid);
                row.RelativeItem().Text(value).FontSize(10);
            });
            col.Item().Height(3);
        }

        private static string BuildLocation(Conference? conf)
        {
            if (conf == null) return "";
            var parts = new[] { conf.Venue, conf.City, conf.Country }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            return string.Join(", ", parts);
        }
    }
}
