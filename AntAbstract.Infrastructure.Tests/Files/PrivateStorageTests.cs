using AntAbstract.Web.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Files;

/// <summary>
/// PrivateStorage.Resolve için whitelist, path traversal ve geriye dönük uyumluluk testleri.
/// </summary>
public sealed class PrivateStorageTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _wwwroot;
    private readonly IWebHostEnvironment _env;

    public PrivateStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"private-storage-tests-{Guid.NewGuid()}");
        _wwwroot = Path.Combine(_tempRoot, "wwwroot");
        Directory.CreateDirectory(_wwwroot);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "private-uploads", "submissions"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "private-uploads", "receipts"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "private-uploads", "templates"));
        _env = new FakeWebHostEnvironment(_tempRoot, _wwwroot);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    // ── Yeni format (private-uploads/) ──────────────────────────────────────

    [Theory]
    [InlineData("private-uploads/submissions/paper.pdf")]
    [InlineData("private-uploads/receipts/receipt.png")]
    [InlineData("private-uploads/templates/template.docx")]
    public void Resolve_NewFormat_ReturnsPathUnderContentRoot(string stored)
    {
        var result = PrivateStorage.Resolve(_env, stored);

        Assert.StartsWith(Path.GetFullPath(_tempRoot), result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(stored.Replace('/', Path.DirectorySeparatorChar), result);
    }

    [Fact]
    public void Resolve_NewFormat_CaseInsensitive()
    {
        var result = PrivateStorage.Resolve(_env, "Private-Uploads/Submissions/paper.pdf");

        Assert.Contains("paper.pdf", result);
    }

    // ── Geriye dönük uyumluluk (eski /uploads/ formatı) ─────────────────────

    [Theory]
    [InlineData("/uploads/submissions/paper.pdf")]
    [InlineData("/uploads/receipts/receipt.jpg")]
    [InlineData("/uploads/templates/template.docx")]
    public void Resolve_LegacyFormat_ReturnsPathUnderWwwroot(string stored)
    {
        var result = PrivateStorage.Resolve(_env, stored);

        Assert.StartsWith(Path.GetFullPath(_wwwroot), result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Path traversal saldırıları ───────────────────────────────────────────

    [Theory]
    [InlineData("private-uploads/submissions/../../appsettings.json")]
    [InlineData("private-uploads/submissions/../../../etc/passwd")]
    [InlineData("/uploads/submissions/../../web.config")]
    [InlineData("private-uploads/../../../etc/shadow")]
    public void Resolve_PathTraversal_ThrowsUnauthorized(string malicious)
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => PrivateStorage.Resolve(_env, malicious));
    }

    // ── İzin verilmeyen yollar ───────────────────────────────────────────────

    [Theory]
    [InlineData("private-uploads/certificates/cert.pdf")]    // beyaz listede yok
    [InlineData("/uploads/proceedings/book.pdf")]             // beyaz listede yok
    [InlineData("/wwwroot/secret.txt")]
    [InlineData("appsettings.json")]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_DisallowedPath_ThrowsUnauthorized(string path)
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => PrivateStorage.Resolve(_env, path));
    }

    [Fact]
    public void Resolve_AbsoluteWindowsPath_ThrowsUnauthorized()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => PrivateStorage.Resolve(_env, @"C:\Windows\System32\drivers\etc\hosts"));
    }

    // ── ToRelativePath ───────────────────────────────────────────────────────

    [Fact]
    public void ToRelativePath_ReturnsCorrectFormat()
    {
        var result = PrivateStorage.ToRelativePath("submissions", "abc123.pdf");

        Assert.Equal("private-uploads/submissions/abc123.pdf", result);
    }

    [Fact]
    public void ToRelativePath_IsResolvable()
    {
        var relative = PrivateStorage.ToRelativePath("submissions", "paper.pdf");
        var physical = PrivateStorage.Resolve(_env, relative);

        Assert.EndsWith("paper.pdf", physical);
        Assert.StartsWith(Path.GetFullPath(_tempRoot), physical, StringComparison.OrdinalIgnoreCase);
    }

    // ── EnsureFolder ─────────────────────────────────────────────────────────

    [Fact]
    public void EnsureFolder_CreatesDirectory_AndReturnsAbsolutePath()
    {
        var newSub = $"submissions/subfolder-{Guid.NewGuid()}";
        var result = PrivateStorage.EnsureFolder(_env, newSub);

        Assert.True(Directory.Exists(result));
        Assert.StartsWith(Path.GetFullPath(_tempRoot), result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Yardımcı sınıf ───────────────────────────────────────────────────────

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string contentRoot, string webRoot)
        {
            ContentRootPath = contentRoot;
            WebRootPath = webRoot;
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
            WebRootFileProvider = new PhysicalFileProvider(webRoot);
        }

        public string ContentRootPath { get; set; }
        public string WebRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
    }
}
