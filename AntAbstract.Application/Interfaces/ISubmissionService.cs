using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntAbstract.Application.Interfaces
{
    public interface ISubmissionService
    {
        Task<SubmissionDto> CreateSubmissionAsync(CreateSubmissionDto input, string userId);

        Task<SubmissionDto> GetSubmissionByIdAsync(Guid id);

        Task<List<SubmissionDto>> GetMySubmissionsAsync(string userId);

        Task<List<SubmissionDto>> GetAllSubmissionsAsync();
        Task UpdateSubmissionAsync(Guid id, CreateSubmissionDto input);
        Task DeleteSubmissionAsync(Guid id);
        Task UpdateStatusAsync(Guid id, SubmissionStatus newStatus);
        Task<List<AntAbstract.Application.DTOs.Conference.ConferenceSelectDto>> GetActiveConferencesAsync();
    }
}