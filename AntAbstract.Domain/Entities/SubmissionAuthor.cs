using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class SubmissionAuthor
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string FirstName { get; set; }
        [Required, StringLength(100)]
        public string LastName { get; set; }

        [StringLength(200)]
        public string? Institution { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? ORCID { get; set; }

        public bool IsCorrespondingAuthor { get; set; } 
        public int Order { get; set; } 

        public Guid SubmissionId { get; set; }

        [ForeignKey("SubmissionId")]
        public Submission Submission { get; set; } = null!;
    }
}