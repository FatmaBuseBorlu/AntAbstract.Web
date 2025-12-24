using AntAbstract.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Submission> Submissions { get; }
        DbSet<SubmissionAuthor> SubmissionAuthors { get; set; }
        DbSet<SubmissionFile> SubmissionFiles { get; set; }
        DbSet<Conference> Conferences{ get; }
        DbSet<Review> Reviews { get; }
        DbSet<ReviewAssignment> ReviewAssignments { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}