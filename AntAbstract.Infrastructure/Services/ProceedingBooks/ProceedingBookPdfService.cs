using AntAbstract.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AntAbstract.Infrastructure.Services.ProceedingBooks
{
    // ─────────────────────────────────────────────────────────────────────────
    //  ProceedingBookPdfService  —  Akademik Bildiri Kitabı (PDF)
    // ─────────────────────────────────────────────────────────────────────────
    public class ProceedingBookPdfService : IProceedingBookPdfService
    {
        // Renk paleti
        private const string Navy = "#1a2d5a";
        private const string Gold = "#b8972a";
        private const string Cream = "#fdf8ee";
        private const string LightBg = "#f4f6fa";
        private const string TextDark = "#1e1e2e";
        private const string TextMid = "#4a4a6a";
        private const string TextLight = "#888899";

        public byte[] GenerateProceedingBookPdf(
            Conference conference,
            IReadOnlyList<Submission> acceptedSubmissions)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var submissions = acceptedSubmissions
                .OrderBy(x => x.ConferenceTopic?.Name ?? x.Topic ?? "")
                .ThenBy(x => x.Title)
                .ToList();

            return Document.Create(container =>
            {
                // ── 1. Kapak Sayfası ───────────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        // Üst lacivert bant
                        col.Item().Height(240).Background(Navy).PaddingVertical(40).PaddingHorizontal(48).Column(top =>
                        {
                            var logoPath = GetExistingFilePath(conference.LogoPath);
                            if (logoPath != null)
                            {
                                top.Item().MaxHeight(72).AlignLeft().Image(logoPath).FitArea();
                                top.Item().Height(20);
                            }

                            top.Item()
                                .Text("BİLDİRİ KİTABI")
                                .FontFamily("Arial").FontSize(10)
                                .LetterSpacing(5).Bold()
                                .FontColor(Gold);

                            top.Item().Height(8);

                            top.Item()
                                .Text(conference.Title ?? "Kongre")
                                .FontFamily("Arial").FontSize(24).Bold()
                                .FontColor(Colors.White)
                                .LineHeight(1.3f);
                        });

                        // Altın bant
                        col.Item().Height(6).Background(Gold);

                        // Orta krem bölge
                        col.Item().Background(Cream).PaddingHorizontal(48).PaddingTop(36).PaddingBottom(24).Column(mid =>
                        {
                            var location = BuildLocation(conference);
                            if (!string.IsNullOrWhiteSpace(location))
                            {
                                mid.Item().Text($"📍  {location}")
                                    .FontFamily("Arial").FontSize(11).FontColor(TextMid);
                                mid.Item().Height(8);
                            }

                            mid.Item().Text($"🗓  {conference.StartDate:dd MMMM yyyy}  –  {conference.EndDate:dd MMMM yyyy}")
                                .FontFamily("Arial").FontSize(11).FontColor(TextMid);

                            mid.Item().Height(32);

                            // İstatistik kutuları
                            mid.Item().Row(row =>
                            {
                                StatBox(row, submissions.Count.ToString(), "Kabul Edilen Bildiri");
                                row.ConstantItem(16);

                                var topicCount = submissions
                                    .Select(s => s.ConferenceTopic?.Name ?? s.Topic ?? "")
                                    .Where(t => !string.IsNullOrWhiteSpace(t))
                                    .Distinct().Count();
                                StatBox(row, topicCount == 0 ? "-" : topicCount.ToString(), "Konu Alanı");
                                row.ConstantItem(16);

                                StatBox(row, DateTime.UtcNow.Year.ToString(), "Yayın Yılı");
                            });

                            mid.Item().Height(36);

                            // Editör / imza bilgisi
                            if (!string.IsNullOrWhiteSpace(conference.CertificateFirstSignerName))
                            {
                                mid.Item().Text("Editör").FontFamily("Arial").FontSize(8)
                                    .FontColor(TextLight).LetterSpacing(2);
                                mid.Item().Height(2);
                                mid.Item().Text(conference.CertificateFirstSignerName)
                                    .FontFamily("Arial").FontSize(13).Bold().FontColor(Navy);
                                if (!string.IsNullOrWhiteSpace(conference.CertificateFirstSignerTitle))
                                {
                                    mid.Item().Text(conference.CertificateFirstSignerTitle)
                                        .FontFamily("Arial").FontSize(10).FontColor(TextMid);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(conference.CertificateSecondSignerName))
                            {
                                mid.Item().Height(8);
                                mid.Item().Text(conference.CertificateSecondSignerName)
                                    .FontFamily("Arial").FontSize(13).Bold().FontColor(Navy);
                                if (!string.IsNullOrWhiteSpace(conference.CertificateSecondSignerTitle))
                                {
                                    mid.Item().Text(conference.CertificateSecondSignerTitle)
                                        .FontFamily("Arial").FontSize(10).FontColor(TextMid);
                                }
                            }
                        });

                        // Alt lacivert bant
                        col.Item().Background(Navy).PaddingHorizontal(48).PaddingVertical(20).Column(foot =>
                        {
                            foot.Item().Text($"Yayın Tarihi: {DateTime.UtcNow:MMMM yyyy}")
                                .FontFamily("Arial").FontSize(9).FontColor(Colors.White);
                        });
                    });
                });

                // ── 2. İçindekiler Sayfası ─────────────────────────────────────
                if (submissions.Any())
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontFamily("Arial"));

                        page.Header().Element(h => PageHeader(h, conference));
                        page.Footer().Element(PageFooter);

                        page.Content().PaddingHorizontal(48).PaddingTop(24).Column(col =>
                        {
                            col.Item().Text("İÇİNDEKİLER")
                                .FontSize(8).LetterSpacing(4).Bold().FontColor(Gold);
                            col.Item().Height(6);
                            col.Item().Text("Contents")
                                .FontSize(22).Bold().FontColor(Navy);
                            col.Item().PaddingTop(4).LineHorizontal(2).LineColor(Gold);
                            col.Item().Height(20);

                            // Konuya göre gruplama
                            var grouped = submissions
                                .GroupBy(s => s.ConferenceTopic?.Name ?? s.Topic ?? "Genel")
                                .OrderBy(g => g.Key)
                                .ToList();

                            foreach (var group in grouped)
                            {
                                col.Item().PaddingTop(12)
                                    .Text(group.Key.ToUpperInvariant())
                                    .FontSize(8).LetterSpacing(2).Bold().FontColor(Gold);

                                col.Item().Height(6);

                                foreach (var s in group)
                                {
                                    col.Item().PaddingVertical(4).Row(row =>
                                    {
                                        row.RelativeItem().Text(text =>
                                        {
                                            text.Span(TruncateText(s.Title, 80))
                                                .FontSize(10).FontColor(TextDark);
                                        });
                                        row.ConstantItem(12);
                                        row.AutoItem().Text(GetAuthorName(s))
                                            .FontSize(9).Italic().FontColor(TextMid);
                                    });
                                    col.Item().PaddingVertical(2).LineHorizontal(0.3f).LineColor("#e0e0e0");
                                }

                                col.Item().Height(8);
                            }
                        });
                    });
                }

                // ── 3. Bildiri Sayfaları ───────────────────────────────────────
                if (!submissions.Any())
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(48);
                        page.DefaultTextStyle(x => x.FontFamily("Arial"));
                        page.Header().Element(h => PageHeader(h, conference));
                        page.Footer().Element(PageFooter);
                        page.Content().AlignCenter().AlignMiddle().Column(col =>
                        {
                            col.Item().AlignCenter()
                                .Text("Henüz kabul edilmiş bildiri bulunmamaktadır.")
                                .FontSize(13).FontColor(TextMid);
                        });
                    });
                    return;
                }

                var submissionGroups = submissions
                    .GroupBy(s => s.ConferenceTopic?.Name ?? s.Topic ?? "Genel")
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var group in submissionGroups)
                {
                    // Konu başlığı ayracı
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);
                        page.DefaultTextStyle(x => x.FontFamily("Arial"));
                        page.Header().Element(h => PageHeader(h, conference));
                        page.Footer().Element(PageFooter);

                        page.Content().PaddingHorizontal(48).PaddingTop(24).Column(col =>
                        {
                            // Konu bölüm başlığı
                            col.Item()
                                .Background(Navy).PaddingVertical(14).PaddingHorizontal(20)
                                .Text(group.Key.ToUpperInvariant())
                                .FontSize(8).LetterSpacing(3).Bold()
                                .FontColor(Colors.White);

                            col.Item().PaddingTop(4)
                                .LineHorizontal(3).LineColor(Gold);

                            col.Item().Height(24);

                            // Bildirileri listele
                            foreach (var submission in group)
                            {
                                col.Item().Element(item => ComposeSubmissionCard(item, submission));
                                col.Item().Height(20);
                            }
                        });
                    });
                }
            }).GeneratePdf();
        }

        // ─── Bildiri Kartı ─────────────────────────────────────────────────────
        private static void ComposeSubmissionCard(IContainer container, Submission submission)
        {
            container
                .Border(0.5f).BorderColor("#d8dce8")
                .Background(Colors.White)
                .Column(col =>
                {
                    // Üst şerit (lacivert sol çizgi)
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(5).Background(Navy);
                        row.RelativeItem().PaddingVertical(14).PaddingHorizontal(18).Column(header =>
                        {
                            header.Item().Text(submission.Title ?? "-")
                                .FontSize(13).Bold().FontColor(Navy).LineHeight(1.3f);

                            header.Item().PaddingTop(4).Text(GetAuthorName(submission))
                                .FontSize(9.5f).Italic().FontColor(Gold);

                            // Meta bilgiler satırı
                            header.Item().PaddingTop(8).Row(meta =>
                            {
                                if (!string.IsNullOrWhiteSpace(submission.SubmissionIdCode))
                                {
                                    MetaBadge(meta, "ID", submission.SubmissionIdCode);
                                    meta.ConstantItem(8);
                                }
                                if (!string.IsNullOrWhiteSpace(submission.PresentationType))
                                {
                                    MetaBadge(meta, "Sunum", submission.PresentationType);
                                    meta.ConstantItem(8);
                                }
                                var topic = submission.ConferenceTopic?.Name ?? submission.Topic;
                                if (!string.IsNullOrWhiteSpace(topic))
                                {
                                    MetaBadge(meta, "Konu", TruncateText(topic, 40));
                                }
                            });
                        });
                    });

                    // Özet bölümü
                    if (!string.IsNullOrWhiteSpace(submission.Abstract))
                    {
                        col.Item().Background(LightBg).PaddingVertical(14).PaddingHorizontal(18).Column(body =>
                        {
                            body.Item().Text("ÖZET")
                                .FontSize(7).LetterSpacing(2).Bold()
                                .FontColor(Gold);

                            body.Item().PaddingTop(5)
                                .Text(CleanText(submission.Abstract))
                                .FontSize(9.5f).FontColor(TextDark)
                                .LineHeight(1.55f);

                            if (!string.IsNullOrWhiteSpace(submission.Keywords))
                            {
                                body.Item().PaddingTop(8).Text(text =>
                                {
                                    text.Span("Anahtar Kelimeler: ").FontSize(8)
                                        .Bold().FontColor(Navy);
                                    text.Span(submission.Keywords).FontSize(8)
                                        .Italic().FontColor(TextMid);
                                });
                            }
                        });
                    }
                });
        }

        // ─── Sayfa Başlığı ─────────────────────────────────────────────────────
        private static void PageHeader(IContainer container, Conference conference)
        {
            container.Column(col =>
            {
                col.Item().PaddingHorizontal(48).PaddingTop(24).Row(row =>
                {
                    row.RelativeItem().Text(conference.Title ?? "Kongre")
                        .FontFamily("Arial").FontSize(9).FontColor(Navy).SemiBold();

                    row.AutoItem().Text("Bildiri Kitabı")
                        .FontFamily("Arial").FontSize(9).FontColor(TextLight);
                });
                col.Item().LineHorizontal(0.5f).LineColor(Gold);
            });
        }

        // ─── Sayfa Altlığı ─────────────────────────────────────────────────────
        private static void PageFooter(IContainer container)
        {
            container.PaddingHorizontal(48).PaddingBottom(16).Column(col =>
            {
                col.Item().LineHorizontal(0.5f).LineColor("#d8dce8");
                col.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text(DateTime.Now.ToString("dd.MM.yyyy"))
                        .FontFamily("Arial").FontSize(8).FontColor(TextLight);

                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Sayfa ").FontFamily("Arial").FontSize(8).FontColor(TextLight);
                        text.CurrentPageNumber().FontFamily("Arial").FontSize(8).FontColor(Navy);
                        text.Span(" / ").FontFamily("Arial").FontSize(8).FontColor(TextLight);
                        text.TotalPages().FontFamily("Arial").FontSize(8).FontColor(Navy);
                    });
                });
            });
        }

        // ─── Stat Kutusu (kapak) ──────────────────────────────────────────────
        private static void StatBox(RowDescriptor row, string value, string label)
        {
            row.RelativeItem()
                .Border(1).BorderColor(Gold)
                .Background(Colors.White)
                .PaddingVertical(14).PaddingHorizontal(12)
                .AlignCenter()
                .Column(c =>
                {
                    c.Item().AlignCenter().Text(value)
                        .FontFamily("Arial").FontSize(22).Bold().FontColor(Navy);
                    c.Item().AlignCenter().PaddingTop(2).Text(label)
                        .FontFamily("Arial").FontSize(8).FontColor(TextMid);
                });
        }

        // ─── Meta Rozet ───────────────────────────────────────────────────────
        private static void MetaBadge(RowDescriptor row, string label, string value)
        {
            row.AutoItem()
                .Background(LightBg)
                .Border(0.5f).BorderColor("#d8dce8")
                .PaddingVertical(3).PaddingHorizontal(7)
                .Text(text =>
                {
                    text.Span($"{label}: ").FontFamily("Arial").FontSize(7.5f)
                        .Bold().FontColor(TextLight);
                    text.Span(value).FontFamily("Arial").FontSize(7.5f)
                        .FontColor(TextDark);
                });
        }

        // ─── Yardımcılar ──────────────────────────────────────────────────────
        private static string BuildLocation(Conference conference)
        {
            var parts = new[] { conference.Venue, conference.City, conference.Country };
            var loc = string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(loc) ? "-" : loc;
        }

        private static string GetAuthorName(Submission submission)
        {
            var name = $"{submission.Author?.FirstName} {submission.Author?.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return submission.Author?.Email ?? "Yazar belirtilmedi";
        }

        private static string CleanText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            return value.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string TruncateText(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            return value.Length <= max ? value : value[..max] + "…";
        }

        private static string? GetExistingFilePath(string? path)
            => !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }
}
