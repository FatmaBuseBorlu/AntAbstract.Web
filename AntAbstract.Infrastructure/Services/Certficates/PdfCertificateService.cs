using AntAbstract.Application.DTOs;
using AntAbstract.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;

namespace AntAbstract.Infrastructure.Services.Certficates
{
    // Bu sınıf sadece PDF üretir.
    // DB, mail, yetki, dosya kaydetme gibi işler burada yapılmaz.
    public sealed class PdfCertificateService
    {
        public byte[] GenerateParticipationCertificate(
            Conference conference,
            AppUser user,
            CertificateType type)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var title = type == CertificateType.Author
                ? "Yazar Katılım Sertifikası"
                : "Hakem Sertifikası";

            var fullName = $"{user.FirstName} {user.LastName}".Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = user.UserName ?? user.Email ?? "Kullanıcı";
            }

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(14));

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item()
                            .Text(conference.Title ?? "Konferans")
                            .FontSize(22)
                            .Bold();

                        column.Item()
                            .Text(title)
                            .FontSize(18)
                            .SemiBold();

                        column.Item()
                            .PaddingTop(20)
                            .Text(fullName)
                            .FontSize(16)
                            .Bold();

                        column.Item()
                            .Text("Bu sertifika ilgili süreç koşulları sağlandığı için oluşturulmuştur.");

                        column.Item()
                            .PaddingTop(20)
                            .Text($"Tarih: {DateTime.UtcNow:dd.MM.yyyy}");
                    });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateAcceptanceCertificate(CertificateDataDto data)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = new CertificateDocument(data);

            return document.GeneratePdf();
        }
    }

    public sealed class CertificateDocument : IDocument
    {
        private readonly CertificateDataDto _model;

        public CertificateDocument(CertificateDataDto model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public DocumentMetadata GetMetadata()
        {
            return DocumentMetadata.Default;
        }

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        var logoPath = GetExistingFilePath(_model.CongressLogoPath);

                        if (logoPath != null)
                        {
                            column.Item()
                                .MaxHeight(60)
                                .Image(logoPath)
                                .FitArea();
                        }

                        column.Item()
                            .PaddingTop(10)
                            .Text(SafeText(_model.CongressName, "Congress"))
                            .SemiBold()
                            .FontSize(14);

                        column.Item()
                            .Text("an Open Access Congress")
                            .FontSize(10);
                    });

                    row.ConstantItem(150);
                });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(25);

                        var laurelPath = GetExistingFilePath(_model.LaurelWreathImagePath);

                        if (laurelPath != null)
                        {
                            column.Item()
                                .AlignCenter()
                                .MaxHeight(80)
                                .Image(laurelPath)
                                .FitArea();
                        }

                        column.Item()
                            .AlignCenter()
                            .Text("CERTIFICATE OF ACCEPTANCE")
                            .Bold()
                            .FontSize(26)
                            .LetterSpacing(2);

                        column.Item()
                            .PaddingTop(1, Unit.Centimetre)
                            .Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(14));

                                text.Span("The certificate of acceptance for the manuscript (");
                                text.Span(SafeText(_model.SubmissionUniqueId, "-")).Bold();
                                text.Span(") titled:");
                            });

                        column.Item()
                            .AlignCenter()
                            .Text($"\"{SafeText(_model.SubmissionTitle, "-")}\"")
                            .FontSize(18)
                            .Italic()
                            .FontColor(Colors.Grey.Darken2);

                        column.Item()
                            .Text("Authored by:")
                            .FontSize(14);

                        column.Item()
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Padding(10)
                            .Text(GetAuthorsText())
                            .FontSize(12);

                        column.Item()
                            .Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(14));

                                text.Span("was accepted in ");

                                text.Span(
                                    $"{SafeText(_model.CongressName, "Congress")} {SafeText(_model.CongressIdentifier, "")}".Trim()
                                ).Bold();

                                text.Span($" on {GetFormattedDate(_model.AcceptanceDate)}.");
                            });
                    });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item()
                            .Text($"{SafeText(_model.CongressLocation, "-")}, {GetMonthYear(_model.AcceptanceDate)}");
                    });

                    row.RelativeItem()
                        .AlignRight()
                        .Column(column =>
                        {
                            var signaturePath = GetExistingFilePath(_model.SignatureImagePath);

                            if (signaturePath != null)
                            {
                                column.Item()
                                    .MaxHeight(50)
                                    .Image(signaturePath)
                                    .FitArea();
                            }

                            column.Item()
                                .LineHorizontal(1)
                                .LineColor(Colors.Grey.Lighten1);

                            column.Item()
                                .Text(SafeText(_model.SignatoryName, ""))
                                .Bold();

                            column.Item()
                                .Text(SafeText(_model.SignatoryTitle, ""));
                        });
                });
            });
        }

        private string GetAuthorsText()
        {
            if (_model.Authors == null || !_model.Authors.Any())
            {
                return "-";
            }

            var authors = _model.Authors
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            return authors.Any()
                ? string.Join("; ", authors)
                : "-";
        }

        private static string SafeText(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        private static string GetFormattedDate(DateTime date)
        {
            return date == default
                ? "-"
                : date.ToString("dd MMMM yyyy");
        }

        private static string GetMonthYear(DateTime date)
        {
            return date == default
                ? "-"
                : date.ToString("MMMM yyyy");
        }

        private static string? GetExistingFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return File.Exists(path)
                ? path
                : null;
        }
    }
}