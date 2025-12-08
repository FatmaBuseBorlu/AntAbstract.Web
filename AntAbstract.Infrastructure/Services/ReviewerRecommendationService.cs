using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Services
{
    public class ReviewerRecommendationService : IReviewerRecommendationService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReviewerRecommendationService(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<AppUser>> GetRecommendationsAsync(Guid submissionId)
        {
           
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null || string.IsNullOrWhiteSpace(submission.Keywords))
            {
                return new List<AppUser>(); 
            }
            var submissionKeywords = submission.Keywords.Split(',')
                .Select(k => k.Trim().ToLower())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            if (!submissionKeywords.Any()) return new List<AppUser>();

            var allReviewers = await _userManager.GetUsersInRoleAsync("Reviewer");

            var recommendations = new Dictionary<AppUser, int>();

            foreach (var reviewer in allReviewers)
            {
                if (string.IsNullOrWhiteSpace(reviewer.ExpertiseAreas))
                {
                    continue; 
                }

                var reviewerExpertise = reviewer.ExpertiseAreas.Split(',')
                    .Select(e => e.Trim().ToLower())
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToList();

                int score = submissionKeywords.Intersect(reviewerExpertise).Count();

                if (score > 0)
                {
                    recommendations[reviewer] = score;
                }
            }
            var sortedRecommendations = recommendations
                .OrderByDescending(pair => pair.Value)
                .Select(pair => pair.Key)
                .ToList();

            return sortedRecommendations;
        }
    }
}