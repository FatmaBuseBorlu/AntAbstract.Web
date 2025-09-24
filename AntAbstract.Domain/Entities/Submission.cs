using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public class Submission
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = null!;
        public string? AbstractText { get; set; }
        public string? FilePath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid ConferenceId { get; set; }
        public Conference Conference { get; set; } = null!;

        public string AuthorId { get; set; } = null!;
        public AppUser Author { get; set; } = null!;
    }
}
