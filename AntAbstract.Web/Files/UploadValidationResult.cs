namespace AntAbstract.Web.Files;

public sealed record UploadValidationResult(
    bool IsValid,
    UploadValidationError Error,
    string Extension,
    string SafeOriginalFileName)
{
    public static UploadValidationResult Valid(
        string extension,
        string safeOriginalFileName)
    {
        return new UploadValidationResult(
            true,
            UploadValidationError.None,
            extension,
            safeOriginalFileName);
    }

    public static UploadValidationResult Invalid(
        UploadValidationError error,
        string extension = "",
        string safeOriginalFileName = "")
    {
        return new UploadValidationResult(
            false,
            error,
            extension,
            safeOriginalFileName);
    }
}
