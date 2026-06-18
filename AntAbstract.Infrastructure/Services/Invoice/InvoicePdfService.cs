using AntAbstract.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;

namespace AntAbstract.Infrastructure.Services.Invoice
{
    public class InvoicePdfService : IInvoicePdfService
    {
        private const string Navy = "#1a2d5a";
        private const string Accent = "#2563eb";
        private const string LightBg = "#f4f6fa";
        private const string TextDark = "#1e1e2e";
        private const string TextMid = "#555577";
        private const string Border = "#dee2e6";

        public byte[] GenerateRegistrationInvoice(Registration reg)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var invoiceNo = BuildInvoiceNumber(reg);
            var conf = reg.Conference;
            var user = reg.AppUser;
            var regType = reg.RegistrationType;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(48);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10).FontColor(TextDark));

                    page.Header().Column(col =>
                    {
                        // Üst bant: logo alanı + başlık
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text("FATURA / INVOICE")
                                    .FontSize(22).Bold().FontColor(Navy);
                                left.Item().Text($"No: {invoiceNo}")
                                    .FontSize(10).FontColor(TextMid);
                            });

                            row.ConstantItem(160).Column(right =>
                            {
                                right.Item().AlignRight().Text(conf?.Title ?? "")
                                    .FontSize(10).Bold().FontColor(Navy);
                                right.Item().AlignRight().Text(
                                    $"{conf?.StartDate:dd.MM.yyyy} – {conf?.EndDate:dd.MM.yyyy}")
                                    .FontSize(9).FontColor(TextMid);
                                var location = BuildLocation(conf);
                                if (!string.IsNullOrWhiteSpace(location))
                                    right.Item().AlignRight().Text(location)
                                        .FontSize(9).FontColor(TextMid);
                            });
                        });

                        col.Item().Height(8);
                        col.Item().BorderBottom(1).BorderColor(Accent).PaddingBottom(4);
                        col.Item().Height(12);
                    });

                    page.Content().Column(col =>
                    {
                        // Fatura bilgileri kutusu
                        col.Item().Row(row =>
                        {
                            // Fatura Tarihi / İşlem No
                            row.RelativeItem().Background(LightBg).Padding(12).Column(left =>
                            {
                                left.Item().Text("FATURA TARİHİ").FontSize(8).Bold().FontColor(TextMid);
                                left.Item().Text((reg.PaymentDate ?? reg.RegistrationDate).ToString("dd.MM.yyyy HH:mm"))
                                    .FontSize(11).Bold();
                                left.Item().Height(8);
                                left.Item().Text("ÖDEME YÖNTEMİ").FontSize(8).Bold().FontColor(TextMid);
                                left.Item().Text("Kredi Kartı / Online Ödeme").FontSize(10);
                            });

                            row.ConstantItem(16);

                            // Faturalanan kişi
                            row.RelativeItem().Background(LightBg).Padding(12).Column(right =>
                            {
                                right.Item().Text("FATURALANAN").FontSize(8).Bold().FontColor(TextMid);
                                right.Item().Text(
                                    !string.IsNullOrWhiteSpace(reg.BillingName)
                                        ? reg.BillingName
                                        : $"{user?.FirstName} {user?.LastName}".Trim())
                                    .FontSize(11).Bold();

                                if (!string.IsNullOrWhiteSpace(reg.TaxNumber))
                                {
                                    right.Item().Height(4);
                                    right.Item().Text($"Vergi No / VKN: {reg.TaxNumber}").FontSize(9);
                                }

                                if (!string.IsNullOrWhiteSpace(reg.TaxOffice))
                                    right.Item().Text($"Vergi Dairesi: {reg.TaxOffice}").FontSize(9);

                                if (!string.IsNullOrWhiteSpace(reg.BillingAddress))
                                {
                                    right.Item().Height(4);
                                    right.Item().Text(reg.BillingAddress).FontSize(9).FontColor(TextMid);
                                }

                                right.Item().Height(4);
                                right.Item().Text(user?.Email ?? "").FontSize(9).FontColor(TextMid);
                            });
                        });

                        col.Item().Height(20);

                        // Kalem tablosu başlığı
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(5);  // Açıklama
                                cols.RelativeColumn(2);  // Para birimi
                                cols.RelativeColumn(2);  // Tutar
                            });

                            // Başlık satırı
                            table.Header(header =>
                            {
                                header.Cell().Background(Navy).Padding(8)
                                    .Text("AÇIKLAMA").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Navy).Padding(8).AlignCenter()
                                    .Text("PARA BİRİMİ").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Navy).Padding(8).AlignRight()
                                    .Text("TUTAR").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            // Kayıt satırı
                            var description = $"{conf?.Title ?? "Kongre"} — {regType?.Name ?? "Kayıt"}";
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingHorizontal(8).PaddingVertical(10)
                                .Text(description).FontSize(10);
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingHorizontal(8).PaddingVertical(10).AlignCenter()
                                .Text(reg.RegistrationType?.Currency ?? "TRY").FontSize(10);
                            table.Cell().BorderBottom(1).BorderColor(Border).PaddingHorizontal(8).PaddingVertical(10).AlignRight()
                                .Text(reg.Amount.ToString("N2")).FontSize(10).Bold();

                            // Boş ara satır
                            table.Cell().PaddingVertical(4).Text("");
                            table.Cell().PaddingVertical(4).Text("");
                            table.Cell().PaddingVertical(4).Text("");

                            // Toplam satırı
                            table.Cell().PaddingHorizontal(8).PaddingVertical(6).AlignRight()
                                .Text("TOPLAM").FontSize(11).Bold();
                            table.Cell().PaddingHorizontal(8).PaddingVertical(6).AlignCenter()
                                .Text(reg.RegistrationType?.Currency ?? "TRY").FontSize(11).Bold();
                            table.Cell().Background(LightBg).PaddingHorizontal(8).PaddingVertical(6).AlignRight()
                                .Text(reg.Amount.ToString("N2")).FontSize(12).Bold().FontColor(Accent);
                        });

                        col.Item().Height(24);

                        // Ödeme durumu
                        var statusColor = reg.IsPaid ? "#198754" : "#dc3545";
                        var statusText = reg.IsPaid ? "ÖDEME ALINDI" : "ÖDEME BEKLENİYOR";
                        col.Item().AlignRight().Text(statusText)
                            .FontSize(13).Bold().FontColor(statusColor);

                        if (reg.PaymentDate.HasValue)
                        {
                            col.Item().Height(4);
                            col.Item().AlignRight()
                                .Text($"Ödeme Tarihi: {reg.PaymentDate.Value:dd.MM.yyyy}")
                                .FontSize(9).FontColor(TextMid);
                        }

                        if (!string.IsNullOrWhiteSpace(reg.PaymentTransactionId))
                        {
                            col.Item().AlignRight()
                                .Text($"İşlem No: {reg.PaymentTransactionId}")
                                .FontSize(9).FontColor(TextMid);
                        }
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().BorderTop(1).BorderColor(Border).PaddingTop(8);
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Bu fatura {DateTime.UtcNow:dd.MM.yyyy} tarihinde elektronik olarak oluşturulmuştur.")
                                .FontSize(8).FontColor(TextMid);
                            row.ConstantItem(60).AlignRight()
                                .Text(x =>
                                {
                                    x.Span("Sayfa ").FontSize(8).FontColor(TextMid);
                                    x.CurrentPageNumber().FontSize(8).FontColor(TextMid);
                                    x.Span(" / ").FontSize(8).FontColor(TextMid);
                                    x.TotalPages().FontSize(8).FontColor(TextMid);
                                });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static string BuildLocation(Conference? conf)
        {
            if (conf == null) return "";
            var parts = new[] { conf.Venue, conf.City, conf.Country }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            return string.Join(", ", parts);
        }

        private static string BuildInvoiceNumber(Registration reg)
        {
            // INV-YYYYMMDD-XXXXXXXX (son 8 hane of Guid)
            var date = (reg.PaymentDate ?? reg.RegistrationDate).ToString("yyyyMMdd");
            var suffix = reg.Id.ToString("N").Substring(0, 8).ToUpper();
            return $"INV-{date}-{suffix}";
        }
    }
}
