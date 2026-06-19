namespace AntAbstract.Infrastructure.Services.Pdf
{
    public interface IPdfAnonymizer
    {
        byte[] AnonymizeMetadata(byte[] pdfBytes, string submissionCode);
    }
}
