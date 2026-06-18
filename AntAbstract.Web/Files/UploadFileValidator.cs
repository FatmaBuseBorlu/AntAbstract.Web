using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AntAbstract.Web.Files;

public sealed class UploadFileValidator : IUploadFileValidator
{
    private const string GenericBinaryContentType = "application/octet-stream";

    private static readonly IReadOnlyDictionary<UploadFileProfile, UploadPolicy> Policies =
        new Dictionary<UploadFileProfile, UploadPolicy>
        {
            [UploadFileProfile.SubmissionDocument] = new(
                10 * 1024 * 1024,
                new[] { ".pdf", ".doc", ".docx" }),
            [UploadFileProfile.ConferenceTemplate] = new(
                10 * 1024 * 1024,
                new[] { ".pdf", ".doc", ".docx" }),
            [UploadFileProfile.PaymentReceipt] = new(
                5 * 1024 * 1024,
                new[] { ".pdf", ".png", ".jpg", ".jpeg" }),
            [UploadFileProfile.ProceedingBookPdf] = new(
                50 * 1024 * 1024,
                new[] { ".pdf" }),
            [UploadFileProfile.RegistrationProfileImage] = new(
                10 * 1024 * 1024,
                new[] { ".jpg", ".jpeg", ".png", ".webp" }),
            [UploadFileProfile.ProfileImage] = new(
                2 * 1024 * 1024,
                new[] { ".jpg", ".jpeg", ".png" }),
            [UploadFileProfile.ConferenceImage] = new(
                5 * 1024 * 1024,
                new[] { ".jpg", ".jpeg", ".png", ".webp" })
        };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> ContentTypes =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = NewContentTypeSet("application/pdf"),
            [".png"] = NewContentTypeSet("image/png"),
            [".jpg"] = NewContentTypeSet("image/jpeg", "image/jpg", "image/pjpeg"),
            [".jpeg"] = NewContentTypeSet("image/jpeg", "image/jpg", "image/pjpeg"),
            [".webp"] = NewContentTypeSet("image/webp"),
            [".doc"] = NewContentTypeSet("application/msword"),
            [".docx"] = NewContentTypeSet(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/zip")
        };

    public async Task<UploadValidationResult> ValidateAsync(
        IFormFile? file,
        UploadFileProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length <= 0)
        {
            return UploadValidationResult.Invalid(UploadValidationError.Empty);
        }

        var policy = Policies[profile];
        var safeOriginalFileName = SanitizeOriginalFileName(file.FileName);
        var extension = Path.GetExtension(safeOriginalFileName).ToLowerInvariant();

        if (!policy.AllowedExtensions.Contains(extension))
        {
            return UploadValidationResult.Invalid(
                UploadValidationError.InvalidExtension,
                extension,
                safeOriginalFileName);
        }

        if (file.Length > policy.MaxBytes)
        {
            return UploadValidationResult.Invalid(
                UploadValidationError.TooLarge,
                extension,
                safeOriginalFileName);
        }

        if (!HasCompatibleContentType(file.ContentType, extension))
        {
            return UploadValidationResult.Invalid(
                UploadValidationError.InvalidContentType,
                extension,
                safeOriginalFileName);
        }

        if (!await HasValidSignatureAsync(file, extension, cancellationToken))
        {
            return UploadValidationResult.Invalid(
                UploadValidationError.InvalidSignature,
                extension,
                safeOriginalFileName);
        }

        return UploadValidationResult.Valid(extension, safeOriginalFileName);
    }

    public string CreateStoredFileName(
        string extension,
        string? prefix = null)
    {
        var normalizedExtension = extension.Trim().ToLowerInvariant();

        if (normalizedExtension.Length < 2 ||
            normalizedExtension[0] != '.' ||
            normalizedExtension.Skip(1).Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new ArgumentException("Invalid file extension.", nameof(extension));
        }

        var safePrefix = string.IsNullOrWhiteSpace(prefix)
            ? ""
            : new string(prefix
                .Where(character => char.IsLetterOrDigit(character) || character == '-')
                .ToArray())
                .Trim('-');

        return string.IsNullOrWhiteSpace(safePrefix)
            ? $"{Guid.NewGuid():N}{normalizedExtension}"
            : $"{safePrefix}-{Guid.NewGuid():N}{normalizedExtension}";
    }

    private static HashSet<string> NewContentTypeSet(params string[] contentTypes)
    {
        return new HashSet<string>(contentTypes, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasCompatibleContentType(
        string? contentType,
        string extension)
    {
        var normalizedContentType = contentType?
            .Split(';', 2)[0]
            .Trim();

        if (string.IsNullOrWhiteSpace(normalizedContentType) ||
            string.Equals(
                normalizedContentType,
                GenericBinaryContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ContentTypes.TryGetValue(extension, out var allowedContentTypes) &&
               allowedContentTypes.Contains(normalizedContentType);
    }

    private static async Task<bool> HasValidSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();

            return extension switch
            {
                ".pdf" => await HasPdfSignatureAsync(stream, cancellationToken),
                ".png" => await StartsWithAsync(
                    stream,
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
                    cancellationToken),
                ".jpg" or ".jpeg" => await StartsWithAsync(
                    stream,
                    new byte[] { 0xFF, 0xD8, 0xFF },
                    cancellationToken),
                ".webp" => await HasWebpSignatureAsync(stream, cancellationToken),
                ".doc" => await StartsWithAsync(
                    stream,
                    new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 },
                    cancellationToken),
                ".docx" => HasDocxStructure(stream),
                _ => false
            };
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task<bool> HasPdfSignatureAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var bytesRead = await ReadAtMostAsync(stream, buffer, cancellationToken);
        var signature = Encoding.ASCII.GetBytes("%PDF-");

        return buffer
            .AsSpan(0, bytesRead)
            .IndexOf(signature) >= 0;
    }

    private static async Task<bool> HasWebpSignatureAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[12];
        var bytesRead = await ReadAtMostAsync(stream, buffer, cancellationToken);

        return bytesRead == buffer.Length &&
               buffer.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
               buffer.AsSpan(8, 4).SequenceEqual("WEBP"u8);
    }

    private static bool HasDocxStructure(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var entryNames = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return entryNames.Contains("[Content_Types].xml") &&
               entryNames.Contains("word/document.xml");
    }

    private static async Task<bool> StartsWithAsync(
        Stream stream,
        byte[] signature,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[signature.Length];
        var bytesRead = await ReadAtMostAsync(stream, buffer, cancellationToken);

        return bytesRead == signature.Length &&
               buffer.AsSpan().SequenceEqual(signature);
    }

    private static async Task<int> ReadAtMostAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }

    private static string SanitizeOriginalFileName(string? fileName)
    {
        var normalizedPath = (fileName ?? "").Replace('\\', '/');
        var leafName = Path.GetFileName(normalizedPath);
        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToHashSet();

        var cleanedName = new string(leafName
            .Where(character =>
                !char.IsControl(character) &&
                !invalidCharacters.Contains(character))
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(cleanedName))
        {
            return "upload";
        }

        if (cleanedName.Length <= 255)
        {
            return cleanedName;
        }

        var extension = Path.GetExtension(cleanedName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(cleanedName);
        var maxNameLength = Math.Max(1, 255 - extension.Length);

        return nameWithoutExtension[..Math.Min(nameWithoutExtension.Length, maxNameLength)] +
               extension;
    }

    private sealed class UploadPolicy
    {
        public UploadPolicy(
            long maxBytes,
            IEnumerable<string> allowedExtensions)
        {
            MaxBytes = maxBytes;
            AllowedExtensions = allowedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public long MaxBytes { get; }

        public HashSet<string> AllowedExtensions { get; }
    }
}
