using AntAbstract.Application.DTOs.Review;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public interface IReviewService
    {
        Task AssignReviewerAsync(AssignReviewerDto input);
        Task SubmitReviewAsync(SubmitReviewDto input, string reviewerName);
        Task<List<ReviewAssignmentDto>> GetMyAssignmentsAsync(string reviewerId);
        Task<ReviewAssignmentDto> GetAssignmentByIdAsync(int id, string reviewerId);

    }
}