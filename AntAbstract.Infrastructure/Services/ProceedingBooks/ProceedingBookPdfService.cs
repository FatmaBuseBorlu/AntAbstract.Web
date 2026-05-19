using AntAbstract.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AntAbstract.Infrastructure.Services.ProceedingBooks
{
    public class ProceedingBookPdfService : IProceedingBookPdfService
    {
        public byte[] GenerateProceedingBookPdf(
            Conference conference,
            IReadOnlyList<Submission> acceptedSubmissions)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var submissions = acceptedSubmissions
                .OrderBy(x => x.Topic)
                .ThenBy(x => x.Title)
                .ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                    page.Header().Element(header =>
                    {
                        ComposeHeader(header, conference);
                    });

                    page.Content().Element(content =>
                    {
                        ComposeContent(content, conference, submissions);
                    });

                    page.Footer().Element(footer =>
                    {
                        ComposeFooter(footer);
                    });
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, Conference conference)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("ANTABSTRACT")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        left.Item().Text("Bildiri Kitabı")
                            .FontSize(26)
                            .Bold()
                            .FontColor(Colors.Grey.Darken4);
                    });

                    row.ConstantItem(130).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy"))
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });

                column.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Blue.Medium);
            });
        }

        private static void ComposeContent(
            IContainer container,
            Conference conference,
            IReadOnlyList<Submission> submissions)
        {
            container.PaddingTop(22).Column(column =>
            {
                ComposeCoverInfo(column, conference);

                column.Item().PaddingTop(22);

                if (!submissions.Any())
                {
                    column.Item().Element(ComposeEmptyState);
                    return;
                }

                for (var i = 0; i < submissions.Count; i++)
                {
                    var submission = submissions[i];

                    column.Item().Element(item =>
                    {
                        ComposeSubmission(item, submission, i + 1);
                    });

                    if (i < submissions.Count - 1)
                    {
                        column.Item().PaddingVertical(12).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    }
                }
            });
        }

        private static void ComposeCoverInfo(ColumnDescriptor column, Conference conference)
        {
            column.Item().Border(1)
                .BorderColor(Colors.Blue.Lighten3)
                .Background(Colors.Blue.Lighten5)
                .Padding(18)
                .Column(info =>
                {
                    info.Item().Text(conference.Title ?? "Kongre")
                        .FontSize(20)
                        .Bold()
                        .FontColor(Colors.Grey.Darken4);

                    if (!string.IsNullOrWhiteSpace(conference.Description))
                    {
                        info.Item().PaddingTop(8).Text(conference.Description)
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken1);
                    }

                    info.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Tarih: ").Bold();
                            text.Span($"{conference.StartDate:dd.MM.yyyy} - {conference.EndDate:dd.MM.yyyy}");
                        });

                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Yer: ").Bold();

                            var location = BuildLocation(conference);
                            text.Span(location);
                        });
                    });
                });
        }

        private static void ComposeEmptyState(IContainer container)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.Grey.Lighten5)
                .Padding(24)
                .AlignCenter()
                .Column(column =>
                {
                    column.Item().Text("Bu kongre için bildiri bulunamadı.")
                        .FontSize(13)
                        .Bold()
                        .FontColor(Colors.Grey.Darken2);

                    column.Item().PaddingTop(6).Text("Bildiri kitabı oluşturmak için en az bir bildirinin kabul edilmiş olması gerekir.")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                });
        }

        private static void ComposeSubmission(
            IContainer container,
            Submission submission,
            int number)
        {
            container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.White)
                .Padding(14)
                .Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(38)
                            .Height(38)
                            .Background(Colors.Blue.Lighten4)
                            .AlignCenter()
                            .AlignMiddle()
                            .Text(number.ToString())
                            .FontSize(11)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        row.RelativeItem().PaddingLeft(12).Column(titleColumn =>
                        {
                            titleColumn.Item().Text(submission.Title)
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Grey.Darken4);

                            titleColumn.Item().PaddingTop(3).Text(GetAuthorName(submission))
                                .FontSize(10)
                                .SemiBold()
                                .FontColor(Colors.Blue.Darken2);
                        });
                    });

                    column.Item().PaddingTop(12).Grid(grid =>
                    {
                        grid.Columns(3);
                        grid.Spacing(8);

                        grid.Item().Element(item =>
                        {
                            ComposeInfoBox(
                                item,
                                "Bildiri Kodu",
                                string.IsNullOrWhiteSpace(submission.SubmissionIdCode)
                                    ? "-"
                                    : submission.SubmissionIdCode);
                        });

                        grid.Item().Element(item =>
                        {
                            ComposeInfoBox(
                                item,
                                "Sunum Türü",
                                string.IsNullOrWhiteSpace(submission.PresentationType)
                                    ? "-"
                                    : submission.PresentationType);
                        });

                        grid.Item().Element(item =>
                        {
                            ComposeInfoBox(
                                item,
                                "Konu / Tema",
                                !string.IsNullOrWhiteSpace(submission.Topic)
                                    ? submission.Topic
                                    : submission.ConferenceTopic?.Name ?? "-");
                        });
                    });

                    column.Item().PaddingTop(12).Text("Özet")
                        .FontSize(10)
                        .Bold()
                        .FontColor(Colors.Grey.Darken4);

                    column.Item()
                        .PaddingTop(5)
                        .Background(Colors.Grey.Lighten5)
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten3)
                        .Padding(10)
                        .Text(CleanText(submission.Abstract))
                        .FontSize(9.5f)
                        .LineHeight(1.35f)
                        .FontColor(Colors.Grey.Darken2);

                    if (!string.IsNullOrWhiteSpace(submission.Keywords))
                    {
                        column.Item().PaddingTop(10).Text(text =>
                        {
                            text.Span("Anahtar Kelimeler: ")
                                .Bold()
                                .FontColor(Colors.Grey.Darken4);

                            text.Span(submission.Keywords)
                                .FontColor(Colors.Grey.Darken2);
                        });
                    }
                });
        }

        private static void ComposeInfoBox(
            IContainer container,
            string label,
            string value)
        {
            container
                .Background(Colors.Grey.Lighten5)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten3)
                .Padding(8)
                .Column(column =>
                {
                    column.Item().Text(label)
                        .FontSize(7.5f)
                        .Bold()
                        .FontColor(Colors.Grey.Darken1);

                    column.Item().PaddingTop(3).Text(value)
                        .FontSize(9)
                        .SemiBold()
                        .FontColor(Colors.Grey.Darken4);
                });
        }

        private static void ComposeFooter(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text("ANTABSTRACT")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Medium);

                row.RelativeItem().AlignCenter().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
                    .FontSize(9)
                    .FontColor(Colors.Grey.Medium);

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Sayfa ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }

        private static string BuildLocation(Conference conference)
        {
            var parts = new[]
            {
                conference.Venue,
                conference.City,
                conference.Country
            };

            var location = string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));

            return string.IsNullOrWhiteSpace(location)
                ? "-"
                : location;
        }

        private static string GetAuthorName(Submission submission)
        {
            var authorName = $"{submission.Author?.FirstName} {submission.Author?.LastName}".Trim();

            if (!string.IsNullOrWhiteSpace(authorName))
            {
                return authorName;
            }

            if (!string.IsNullOrWhiteSpace(submission.Author?.Email))
            {
                return submission.Author.Email;
            }

            return "Yazar belirtilmedi";
        }

        private static string CleanText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
        }
    }
}