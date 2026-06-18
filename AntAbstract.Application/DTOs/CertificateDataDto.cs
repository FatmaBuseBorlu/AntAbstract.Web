using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Application.DTOs
{
    public class CertificateDataDto
    {
        public string SubmissionUniqueId { get; set; } = string.Empty;
        public string SubmissionTitle { get; set; } = string.Empty;
        public List<string> Authors { get; set; } = new();
        public string CongressName { get; set; } = string.Empty;
        public string CongressIdentifier { get; set; } = string.Empty;
        public DateTime AcceptanceDate { get; set; }
        public string CongressLocation { get; set; } = string.Empty;
        public string SignatoryName { get; set; } = string.Empty;
        public string SignatoryTitle { get; set; } = string.Empty;
        public string CongressLogoPath { get; set; } = string.Empty;
        public string LaurelWreathImagePath { get; set; } = string.Empty;
        public string SignatureImagePath { get; set; } = string.Empty;
    }
}
