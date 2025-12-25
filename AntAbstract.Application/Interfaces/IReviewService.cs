using AntAbstract.Application.DTOs.Review;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public interface IReviewService
    {
        Task AssignReviewerAsync(AssignReviewerDto input);

        Task<List<ReviewAssignmentDto>> GetMyAssignmentsAsync(string reviewerId);

        Task<ReviewAssignmentDto> GetAssignmentByIdAsync(int id, string reviewerId);

        Task SubmitReviewAsync(SubmitReviewDto input, string reviewerName);

        Task<List<ReviewAssignmentDto>> GetReviewsBySubmissionIdAsync(Guid submissionId);
    }
}