using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AntAbstract.Domain;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Domain.Entities
{
    public class Submission
    {
        public Guid SubmissionId { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = null!;
        public string? AbstractText { get; set; }

        [BindNever]
        public string Status { get; set; } = "Yeni Gönderildi";

        [BindNever]
        public string? FilePath { get; set; }
        [BindNever]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 
        [BindNever]
        public string? FinalDecision { get; set; } 
        [BindNever]
        public DateTime? DecisionDate { get; set; } 

        [BindNever]
        public Guid ConferenceId { get; set; }
        public Conference? Conference { get; set; } 

        [BindNever]
        public string AuthorId { get; set; } = null!;
        public AppUser? Author { get; set; } 

        public ICollection<ReviewAssignment> ReviewAssignments { get; set; } = new List<ReviewAssignment>();
    }
}
