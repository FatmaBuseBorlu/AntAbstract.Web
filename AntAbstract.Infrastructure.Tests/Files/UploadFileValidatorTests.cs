using System.IO.Compression;
using System.Text;
using AntAbstract.Web.Files;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Files;

public sealed class UploadFileValidatorTests
{
    private readonly UploadFileValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_AcceptsPdfWithMatchingSignature()
    {
        var file = CreateFile(
            "%PDF-1.7\nsample"u8.ToArray(),
            "paper.pdf",
            "application/pdf");

        var result = await _validator.ValidateAsync(
            file,
            UploadFileProfile.SubmissionDocument);

        Assert.True(result.IsValid);
        Assert.Equal(".pdf", result.Extension);
    }

    [Fact]
    public async Task ValidateAsync_RejectsSpoofedPdf()
    {
        var file = CreateFile(
            Encoding.UTF8.GetBytes("<script>alert('x')</script>"),
            "paper.pdf",
            "application/pdf");

        var result = await _validator.ValidateAsync(
            file,
            UploadFileProfile.SubmissionDocument);

        Assert.False(result.IsValid);
        Assert.Equal(UploadValidationError.InvalidSignature, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_RejectsMismatchedContentType()
    {
        var file = CreateFile(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            "receipt.png",
            "text/plain");

        var result = await _validator.ValidateAsync(
            file,
            UploadFileProfile.PaymentReceipt);

        Assert.False(result.IsValid);
        Assert.Equal(UploadValidationError.InvalidContentType, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsDocxWithRequiredStructure()
    {
        var file = CreateDocxFile();

        var result = await _validator.ValidateAsync(
            file,
            UploadFileProfile.ConferenceTemplate);

        Assert.True(result.IsValid);
        Assert.Equal(".docx", result.Extension);
    }

    [Fact]
    public async Task ValidateAsync_RejectsImageAboveProfileLimit()
    {
        var bytes = new byte[(2 * 1024 * 1024) + 1];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        var file = CreateFile(bytes, "profile.jpg", "image/jpeg");

        var result = await _validator.ValidateAsync(
            file,
            UploadFileProfile.ProfileImage);

        Assert.False(result.IsValid);
        Assert.Equal(UploadValidationError.TooLarge, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_RemovesPathAndUnsafeCharactersFromOriginalName()
    {
        var file = CreateFile(
            "%PDF-1.7\nsample"u8.ToArray(),
            @"C:\temp\<paper>.pdf",
            "application/pdf");

        var result = await _validator.ValidateAsync(
            file,
            UploadFileProfile.SubmissionDocument);

        Assert.True(result.IsValid);
        Assert.Equal("paper.pdf", result.SafeOriginalFileName);
    }

    [Fact]
    public void CreateStoredFileName_UsesOnlyServerControlledParts()
    {
        var fileName = _validator.CreateStoredFileName(
            ".pdf",
            "../proceeding book");

        Assert.StartsWith("proceedingbook-", fileName);
        Assert.EndsWith(".pdf", fileName);
        Assert.DoesNotContain("..", fileName);
        Assert.DoesNotContain("/", fileName);
    }

    private static FormFile CreateFile(
        byte[] content,
        string fileName,
        string contentType)
    {
        var stream = new MemoryStream(content);

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static FormFile CreateDocxFile()
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", "<Types />");
            WriteEntry(archive, "word/document.xml", "<document />");
        }

        stream.Position = 0;

        return new FormFile(stream, 0, stream.Length, "file", "template.docx")
        {
            Headers = new HeaderDictionary(),
            ContentType =
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static void WriteEntry(
        ZipArchive archive,
        string entryName,
        string content)
    {
        var entry = archive.CreateEntry(entryName);

        using var writer = new StreamWriter(
            entry.Open(),
            Encoding.UTF8,
            leaveOpen: false);

        writer.Write(content);
    }
}
