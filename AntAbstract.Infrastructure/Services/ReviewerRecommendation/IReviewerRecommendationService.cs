using AntAbstract.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services.ReviewerRecommendation
{
    public interface IReviewerRecommendationService
    {
    
        Task<List<AppUser>> GetRecommendationsAsync(Guid submissionId);
    }
}