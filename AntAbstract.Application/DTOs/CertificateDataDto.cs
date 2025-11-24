using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Application.DTOs
{
    public class CertificateDataDto
    {
        public string SubmissionUniqueId { get; set; }
        public string SubmissionTitle { get; set; }
        public List<string> Authors { get; set; }
        public string CongressName { get; set; }
        public string CongressIdentifier { get; set; }
        public DateTime AcceptanceDate { get; set; }
        public string CongressLocation { get; set; }
        public string SignatoryName { get; set; }
        public string SignatoryTitle { get; set; }
        public string CongressLogoPath { get; set; }
        public string LaurelWreathImagePath { get; set; }
        public string SignatureImagePath { get; set; }
    }
}
