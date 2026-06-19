using AntAbstract.Infrastructure.Services.Pdf;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class PdfAnonymizerTests
{
    [Fact]
    public void AnonymizeMetadata_ClearsAuthorAndTitle()
    {
        var pdfBytes = CreateSamplePdf("Dr. John Smith", "Secret Research Paper");
        var anonymizer = new PdfAnonymizer();

        var result = anonymizer.AnonymizeMetadata(pdfBytes, "SUB-ABC12345");

        using var stream = new MemoryStream(result);
        using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.InformationOnly);

        Assert.Equal("", doc.Info.Author);
        Assert.Equal("SUB-ABC12345", doc.Info.Title);
        Assert.Equal("AntAbstract", doc.Info.Creator);
        Assert.Equal("", doc.Info.Subject);
        Assert.Equal("", doc.Info.Keywords);
    }

    [Fact]
    public void AnonymizeMetadata_PreservesPageCount()
    {
        var pdfBytes = CreateSamplePdf("Author", "Title");
        var anonymizer = new PdfAnonymizer();

        var result = anonymizer.AnonymizeMetadata(pdfBytes, "CODE");

        using var stream = new MemoryStream(result);
        using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.InformationOnly);

        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void AnonymizeMetadata_OutputIsValidPdf()
    {
        var pdfBytes = CreateSamplePdf("Test Author", "Test Title");
        var anonymizer = new PdfAnonymizer();

        var result = anonymizer.AnonymizeMetadata(pdfBytes, "TEST");

        Assert.True(result.Length > 0);
        Assert.Equal(0x25, result[0]); // '%' - PDF magic byte
        Assert.Equal(0x50, result[1]); // 'P'
        Assert.Equal(0x44, result[2]); // 'D'
        Assert.Equal(0x46, result[3]); // 'F'
    }

    private static byte[] CreateSamplePdf(string author, string title)
    {
        var doc = new PdfDocument();
        doc.Info.Author = author;
        doc.Info.Title = title;
        doc.Info.Subject = "Test Subject";
        doc.Info.Keywords = "test, sample";
        doc.AddPage();

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }
}
