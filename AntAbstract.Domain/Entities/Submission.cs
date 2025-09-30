using AntAbstract.Domain;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public Guid? SessionId { get; set; } 

        [ForeignKey("SessionId")]
        public Session? Session { get; set; }

        public ICollection<ReviewAssignment> ReviewAssignments { get; set; } = new List<ReviewAssignment>();

        [StringLength(500)]
        public string? Keywords { get; set; } // Örn: "yapay zeka, makine öğrenmesi, NLP"
    }
}
