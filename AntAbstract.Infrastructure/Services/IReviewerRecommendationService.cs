// AntAbstract.Infrastructure/Services/IReviewerRecommendationService.cs
using AntAbstract.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services
{
    public interface IReviewerRecommendationService
    {
        // Bir özet ID'si alıp, o özete uygun hakemlerin listesini dönen metot.
        Task<List<AppUser>> GetRecommendationsAsync(Guid submissionId);
    }
}