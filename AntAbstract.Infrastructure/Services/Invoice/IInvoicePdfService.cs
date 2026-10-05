using AntAbstract.Domain.Entities;

namespace AntAbstract.Infrastructure.Services.Invoice
{
    public interface IInvoicePdfService
    {
        /// <summary>Ödeme makbuzu (fatura değil). paymentMethod: Payment.PaymentMethod.</summary>
        byte[] GenerateRegistrationInvoice(Registration registration, string? paymentMethod = null);
    }
}
