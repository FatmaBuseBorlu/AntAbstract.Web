using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Infrastructure.Services.ProceedingBooks
{
    public interface IProceedingBookPdfService
    {
        byte[] GenerateProceedingBookPdf(
            Conference conference,
            IReadOnlyList<Submission> acceptedSubmissions);
    }
}