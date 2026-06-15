using Microsoft.AspNetCore.Http;

namespace AntAbstract.Web.Files;

public interface IUploadFileValidator
{
    Task<UploadValidationResult> ValidateAsync(
        IFormFile? file,
        UploadFileProfile profile,
        CancellationToken cancellationToken = default);

    string CreateStoredFileName(
        string extension,
        string? prefix = null);
}
