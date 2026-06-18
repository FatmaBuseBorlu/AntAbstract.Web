using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Services.Invoice
{
    public interface IInvoicePdfService
    {
        byte[] GenerateRegistrationInvoice(Registration registration);
    }
}
