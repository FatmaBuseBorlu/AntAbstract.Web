using AntAbstract.Infrastructure.Services.Pdf;
using System.Text;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class PdfAnonymizerTests
{
    [Fact]
    public void AnonymizeMetadata_ClearsAuthorAndTitle()
    {
        var pdfBytes = CreateMinimalPdf("Dr. John Smith", "Secret Research Paper");
        var anonymizer = new PdfAnonymizer();

        var result = anonymizer.AnonymizeMetadata(pdfBytes, "SUB-ABC12345");
        var text = Encoding.Latin1.GetString(result);

        Assert.Contains("/Author ()", text);
        Assert.Contains("/Title (SUB-ABC12345)", text);
        Assert.Contains("/Creator (AntAbstract)", text);
        Assert.Contains("/Subject ()", text);
        Assert.Contains("/Keywords ()", text);
        Assert.DoesNotContain("Dr. John Smith", text);
        Assert.DoesNotContain("Secret Research Paper", text);
    }

    [Fact]
    public void AnonymizeMetadata_OutputIsValidPdf()
    {
        var pdfBytes = CreateMinimalPdf("Test Author", "Test Title");
        var anonymizer = new PdfAnonymizer();

        var result = anonymizer.AnonymizeMetadata(pdfBytes, "TEST");

        Assert.True(result.Length > 0);
        Assert.Equal((byte)'%', result[0]);
        Assert.Equal((byte)'P', result[1]);
        Assert.Equal((byte)'D', result[2]);
        Assert.Equal((byte)'F', result[3]);
    }

    [Fact]
    public void AnonymizeMetadata_PreservesPdfStructure()
    {
        var pdfBytes = CreateMinimalPdf("Author Name", "Paper Title");
        var anonymizer = new PdfAnonymizer();

        var result = anonymizer.AnonymizeMetadata(pdfBytes, "CODE-123");
        var text = Encoding.Latin1.GetString(result);

        Assert.Contains("%%EOF", text);
        Assert.Contains("/Type /Catalog", text);
    }

    [Fact]
    public void AnonymizeMetadata_HandlesProducerField()
    {
        var pdf = CreateMinimalPdf("Author", "Title");
        var text = Encoding.Latin1.GetString(pdf);
        text = text.Replace("/Keywords (kw)", "/Keywords (kw)\n/Producer (Microsoft Word)");
        var pdfWithProducer = Encoding.Latin1.GetBytes(text);

        var anonymizer = new PdfAnonymizer();
        var result = anonymizer.AnonymizeMetadata(pdfWithProducer, "X");
        var resultText = Encoding.Latin1.GetString(result);

        Assert.Contains("/Producer (AntAbstract)", resultText);
        Assert.DoesNotContain("Microsoft Word", resultText);
    }

    private static byte[] CreateMinimalPdf(string author, string title)
    {
        var pdf = $@"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj

2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj

3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>
endobj

4 0 obj
<< /Author ({author}) /Title ({title}) /Creator (TestApp) /Subject (Test Subject) /Keywords (kw) >>
endobj

xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000206 00000 n

trailer
<< /Size 5 /Root 1 0 R /Info 4 0 R >>
startxref
350
%%EOF";
        return Encoding.Latin1.GetBytes(pdf);
    }
}
