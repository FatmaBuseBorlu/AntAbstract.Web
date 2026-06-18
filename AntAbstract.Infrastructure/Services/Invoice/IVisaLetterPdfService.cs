using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Services.Invoice
{
    public interface IVisaLetterPdfService
    {
        byte[] GenerateVisaLetter(Registration registration);
    }
}
