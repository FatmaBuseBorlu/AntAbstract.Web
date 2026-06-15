namespace AntAbstract.Web.Files;

public enum UploadValidationError
{
    None,
    Empty,
    TooLarge,
    InvalidExtension,
    InvalidContentType,
    InvalidSignature
}
