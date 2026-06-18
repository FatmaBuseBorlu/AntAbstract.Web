using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Application.DTOs.Submission
{
    public class SubmissionAuthorDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;

        public string? ORCID { get; set; }

        public bool IsCorrespondingAuthor { get; set; }
        public int Order { get; set; }
    }
}
