using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using AntAbstract.Domain;

namespace AntAbstract.Domain.Entities
{
    public class Submission
    {

            public Guid SubmissionId { get; set; } = Guid.NewGuid();
            public string Title { get; set; } = null!;
            public string? AbstractText { get; set; }

            [BindNever]
            public string? FilePath { get; set; }

            [BindNever]
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            [BindNever]
            public Guid ConferenceId { get; set; }
            public Conference? Conference { get; set; }

            [BindNever]
            public string AuthorId { get; set; } = null!;
            public AppUser? Author { get; set; } = null!;

    }
}
