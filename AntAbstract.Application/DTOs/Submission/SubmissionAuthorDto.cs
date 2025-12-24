using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Application.DTOs.Submission
{
    public class SubmissionAuthorDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Institution { get; set; }

        public string? ORCID { get; set; }

        public bool IsCorrespondingAuthor { get; set; }
        public int Order { get; set; }
    }
}
