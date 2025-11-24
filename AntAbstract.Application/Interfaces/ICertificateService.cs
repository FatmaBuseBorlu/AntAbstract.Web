using AntAbstract.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public interface ICertificateService
    {
        byte[] GenerateAcceptanceCertificate(CertificateDataDto data);
    }
}
