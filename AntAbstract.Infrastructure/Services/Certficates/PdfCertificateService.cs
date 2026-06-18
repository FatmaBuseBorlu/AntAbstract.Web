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
    // ─────────────────────────────────────────────────────────────────────────
    //  PdfCertificateService  —  Katılım & Kabul Sertifikası PDF üretimi
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class PdfCertificateService
    {
        /// <summary>
        /// Katılım sertifikası (Yazar / Hakem / Dinleyici)
        /// A4 Yatay — iki imza satırı destekler
        /// </summary>
        public byte[] GenerateParticipationCertificate(
            Conference conference,
            AppUser user,
            CertificateType type)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var roleLabel = type switch
            {
                CertificateType.Author   => "Yazar",
                CertificateType.Reviewer => "Hakem",
                CertificateType.Attendee => "Dinleyici",
                _                        => "Katılımcı"
            };

            var participationText = type switch
            {
                CertificateType.Author =>
                    "adlı bilim insanının, aşağıda bilgileri verilen kongrede bildiri sunarak katkı sağladığını belgeleriz.",
                CertificateType.Reviewer =>
                    "adlı bilim insanının, aşağıda bilgileri verilen kongrede hakem olarak görev yaptığını belgeleriz.",
                CertificateType.Attendee =>
                    "adlı katılımcının, aşağıda bilgileri verilen kongreye dinleyici olarak iştirak ettiğini belgeleriz.",
                _ =>
                    "adlı kişinin aşağıda bilgileri verilen kongreye katıldığını belgeleriz."
            };

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
                fullName = user.UserName ?? user.Email ?? "Katılımcı";

            var location = BuildLocation(conference);
            var dateRange = $"{conference.StartDate:dd MMMM yyyy} – {conference.EndDate:dd MMMM yyyy}";

            // Renk paleti  ─ koyu lacivert + altın
            var navy   = "#1a2d5a";
            var gold   = "#b8972a";
            var cream  = "#fdf8ee";
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(0);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).FontColor(navy));

                    page.Content().Column(stack =>
                    {
                        // ── Arka plan krem rengi + dış çerçeve ──────────────────
                        stack.Item().Background(cream).Padding(24).Column(root =>
                        {
                            // Dış çift çerçeve
                            root.Item().Border(4).BorderColor(navy)
                                .Padding(6)
                                .Border(1.5f).BorderColor(gold)
                                .Padding(28)
                                .Column(inner =>
                                {
                                    // ── ÜST BANT: Logo + Başlık ──────────────────
                                    inner.Item().Row(row =>
                                    {
                                        // Logo (varsa)
                                        var logoPath = GetExistingFilePath(conference.LogoPath);
                                        if (logoPath != null)
                                        {
                                            row.ConstantItem(90).AlignMiddle()
                                                .Image(logoPath).FitArea();
                                            row.ConstantItem(20);
                                        }

                                        row.RelativeItem().AlignCenter().Column(titleCol =>
                                        {
                                            titleCol.Item().AlignCenter()
                                                .Text("SERTİFİKA")
                                                .FontSize(11)
                                                .LetterSpacing(5)
                                                .FontColor(gold)
                                                .Bold();

                                            titleCol.Item().AlignCenter().PaddingTop(4)
                                                .Text(conference.Title ?? "Kongre")
                                                .FontSize(18)
                                                .Bold()
                                                .FontColor(navy);

                                            titleCol.Item().AlignCenter().PaddingTop(2)
                                                .Text($"{location}  ·  {dateRange}")
                                                .FontSize(9)
                                                .FontColor("#666666");
                                        });
                                    });

                                    // ── Altın ayraç çizgisi ───────────────────────
                                    inner.Item().PaddingTop(16).PaddingBottom(16)
                                        .LineHorizontal(1.5f).LineColor(gold);

                                    // ── ROL etiketi ───────────────────────────────
                                    inner.Item().AlignCenter()
                                        .Background(navy).PaddingVertical(6).PaddingHorizontal(14)
                                        .Text($"[ {roleLabel.ToUpperInvariant()} KATILIM SERTİFİKASI ]")
                                        .FontSize(9)
                                        .LetterSpacing(2)
                                        .FontColor(Colors.White)
                                        .Bold();

                                    // ── Gövde metni ───────────────────────────────
                                    inner.Item().PaddingTop(28).AlignCenter()
                                        .Text("Bu belge;")
                                        .FontSize(11)
                                        .FontColor("#444444");

                                    // ── İsim ──────────────────────────────────────
                                    inner.Item().PaddingTop(14).AlignCenter()
                                        .Text(fullName)
                                        .FontSize(30)
                                        .Bold()
                                        .FontColor(navy)
                                        .LetterSpacing(1);

                                    // ── Açıklama ──────────────────────────────────
                                    inner.Item().PaddingTop(14).AlignCenter()
                                        .Text(participationText)
                                        .FontSize(11)
                                        .FontColor("#444444")
                                        .LineHeight(1.5f);

                                    // ── Altın ayraç ───────────────────────────────
                                    inner.Item().PaddingTop(24).PaddingBottom(24)
                                        .LineHorizontal(0.5f).LineColor(gold);

                                    // ── Tarih + İmzalar ───────────────────────────
                                    inner.Item().Row(signRow =>
                                    {
                                        // Tarih (sol)
                                        signRow.RelativeItem().AlignLeft().Column(dateCol =>
                                        {
                                            dateCol.Item()
                                                .Text($"Düzenlenme Tarihi")
                                                .FontSize(8).FontColor("#888888");
                                            dateCol.Item().PaddingTop(3)
                                                .Text(DateTime.UtcNow.ToString("dd MMMM yyyy"))
                                                .FontSize(11).Bold().FontColor(navy);
                                        });

                                        // Sertifika No (orta)
                                        signRow.RelativeItem().AlignCenter().Column(codeCol =>
                                        {
                                            codeCol.Item().AlignCenter()
                                                .Text("Sertifika No")
                                                .FontSize(8).FontColor("#888888");
                                            codeCol.Item().PaddingTop(3).AlignCenter()
                                                .Text(GenerateCertCode(fullName, conference.Id))
                                                .FontSize(9).FontColor("#555555").FontFamily("Courier New");
                                        });

                                        // İmzalar (sağ) — 1. imza
                                        signRow.RelativeItem().AlignRight().Column(sig1 =>
                                        {
                                            ComposeSignature(sig1, conference.CertificateFirstSignerName, conference.CertificateFirstSignerTitle, gold, navy);
                                        });

                                        // 2. imza (varsa)
                                        if (!string.IsNullOrWhiteSpace(conference.CertificateSecondSignerName))
                                        {
                                            signRow.ConstantItem(24);
                                            signRow.RelativeItem().AlignRight().Column(sig2 =>
                                            {
                                                ComposeSignature(sig2, conference.CertificateSecondSignerName, conference.CertificateSecondSignerTitle, gold, navy);
                                            });
                                        }
                                    });
                                });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static void ComposeSignature(
            ColumnDescriptor col,
            string? name,
            string? title,
            string gold,
            string navy)
        {
            col.Item().AlignCenter()
                .LineHorizontal(0.8f).LineColor(gold);
            col.Item().PaddingTop(4).AlignCenter()
                .Text(string.IsNullOrWhiteSpace(name) ? "İmza" : name)
                .FontSize(10).Bold().FontColor(navy);
            if (!string.IsNullOrWhiteSpace(title))
            {
                col.Item().AlignCenter()
                    .Text(title)
                    .FontSize(8).FontColor("#666666");
            }
        }

        private static string GenerateCertCode(string fullName, Guid conferenceId)
        {
            var hash = Math.Abs(HashCode.Combine(fullName, conferenceId));
            return $"CERT-{hash:X8}";
        }

        // ─── Kabul Mektubu (var olan CertificateDocument ile) ────────────────
        public byte[] GenerateAcceptanceCertificate(CertificateDataDto data)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return new CertificateDocument(data).GeneratePdf();
        }

        private static string BuildLocation(Conference conference)
        {
            var parts = new[] { conference.Venue, conference.City, conference.Country };
            var loc = string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(loc) ? "" : loc;
        }

        private static string? GetExistingFilePath(string? path)
            => !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CertificateDocument  —  Kabul Belgesi (submission için)
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class CertificateDocument : IDocument
    {
        private readonly CertificateDataDto _model;

        public CertificateDocument(CertificateDataDto model)
            => _model = model ?? throw new ArgumentNullException(nameof(model));

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).FontColor("#1a2d5a"));

                page.Content().Column(stack =>
                {
                    stack.Item().Background("#fdf8ee").Padding(24).Column(root =>
                    {
                        root.Item().Border(4).BorderColor("#1a2d5a")
                            .Padding(6)
                            .Border(1.5f).BorderColor("#b8972a")
                            .Padding(28)
                            .Column(inner =>
                            {
                                // ── Üst: Logo + Başlık ───────────────────────────
                                inner.Item().Row(row =>
                                {
                                    var logoPath = GetExistingFilePath(_model.CongressLogoPath);
                                    if (logoPath != null)
                                    {
                                        row.ConstantItem(80).AlignMiddle().Image(logoPath).FitArea();
                                        row.ConstantItem(16);
                                    }

                                    row.RelativeItem().AlignCenter().Column(tc =>
                                    {
                                        tc.Item().AlignCenter()
                                            .Text("CERTIFICATE OF ACCEPTANCE")
                                            .FontSize(9).LetterSpacing(4)
                                            .FontColor("#b8972a").Bold();

                                        tc.Item().AlignCenter().PaddingTop(4)
                                            .Text(SafeText(_model.CongressName, "Congress"))
                                            .FontSize(16).Bold().FontColor("#1a2d5a");

                                        if (!string.IsNullOrWhiteSpace(_model.CongressIdentifier))
                                        {
                                            tc.Item().AlignCenter().PaddingTop(2)
                                                .Text(_model.CongressIdentifier)
                                                .FontSize(9).FontColor("#666666");
                                        }
                                    });
                                });

                                inner.Item().PaddingTop(18).PaddingBottom(18)
                                    .LineHorizontal(1.5f).LineColor("#b8972a");

                                // ── Gövde ────────────────────────────────────────
                                inner.Item().AlignCenter()
                                    .Text("This is to certify that the manuscript")
                                    .FontSize(11).FontColor("#444444");

                                // Bildiri başlığı
                                inner.Item().PaddingTop(14).AlignCenter()
                                    .Text($"“{SafeText(_model.SubmissionTitle, "-")}”")
                                    .FontSize(17).Bold().Italic()
                                    .FontColor("#1a2d5a");

                                // Submission ID
                                if (!string.IsNullOrWhiteSpace(_model.SubmissionUniqueId))
                                {
                                    inner.Item().PaddingTop(6).AlignCenter()
                                        .Text($"(Manuscript ID: {_model.SubmissionUniqueId})")
                                        .FontSize(9).FontColor("#888888");
                                }

                                inner.Item().PaddingTop(14).AlignCenter()
                                    .Text("authored by:")
                                    .FontSize(11).FontColor("#444444");

                                // Yazarlar
                                inner.Item().PaddingTop(8).AlignCenter()
                                    .Text(GetAuthorsText())
                                    .FontSize(13).Bold().FontColor("#1a2d5a");

                                inner.Item().PaddingTop(16).AlignCenter()
                                    .Text(text =>
                                    {
                                        text.DefaultTextStyle(x => x.FontSize(11).FontColor("#444444"));
                                        text.Span("has been accepted for presentation at ");
                                        text.Span(SafeText(_model.CongressName, "")).Bold().FontColor("#1a2d5a");
                                        text.Span($" on {GetFormattedDate(_model.AcceptanceDate)}.");
                                    });

                                inner.Item().PaddingTop(24).PaddingBottom(24)
                                    .LineHorizontal(0.5f).LineColor("#b8972a");

                                // ── Footer: Yer/Tarih + İmza ─────────────────────
                                inner.Item().Row(footRow =>
                                {
                                    footRow.RelativeItem().AlignLeft().Column(loc =>
                                    {
                                        loc.Item().Text("Location & Date").FontSize(8).FontColor("#888888");
                                        loc.Item().PaddingTop(3)
                                            .Text($"{SafeText(_model.CongressLocation, "-")}")
                                            .FontSize(10).Bold().FontColor("#1a2d5a");
                                        loc.Item()
                                            .Text(GetMonthYear(_model.AcceptanceDate))
                                            .FontSize(10).FontColor("#555555");
                                    });

                                    footRow.RelativeItem().AlignRight().Column(sigCol =>
                                    {
                                        var sigPath = GetExistingFilePath(_model.SignatureImagePath);
                                        if (sigPath != null)
                                        {
                                            sigCol.Item().AlignCenter().MaxHeight(48)
                                                .Image(sigPath).FitArea();
                                        }

                                        sigCol.Item().AlignCenter()
                                            .LineHorizontal(0.8f).LineColor("#b8972a");
                                        sigCol.Item().PaddingTop(4).AlignCenter()
                                            .Text(SafeText(_model.SignatoryName, "")).FontSize(10).Bold().FontColor("#1a2d5a");
                                        if (!string.IsNullOrWhiteSpace(_model.SignatoryTitle))
                                        {
                                            sigCol.Item().AlignCenter()
                                                .Text(_model.SignatoryTitle).FontSize(8).FontColor("#666666");
                                        }
                                    });
                                });
                            });
                    });
                });
            });
        }

        private string GetAuthorsText()
        {
            if (_model.Authors == null || !_model.Authors.Any()) return "-";
            return string.Join("  ·  ", _model.Authors.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        }

        private static string SafeText(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string GetFormattedDate(DateTime date)
            => date == default ? "-" : date.ToString("dd MMMM yyyy");

        private static string GetMonthYear(DateTime date)
            => date == default ? "-" : date.ToString("MMMM yyyy");

        private static string? GetExistingFilePath(string? path)
            => !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }
}
