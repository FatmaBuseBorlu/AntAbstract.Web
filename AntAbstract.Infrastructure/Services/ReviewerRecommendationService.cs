// AntAbstract.Infrastructure/Services/ReviewerRecommendationService.cs
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
            // 1. İlgili özeti bul ve anahtar kelimelerini al.
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission == null || string.IsNullOrWhiteSpace(submission.Keywords))
            {
                return new List<AppUser>(); // Özet yoksa veya anahtar kelimesi yoksa boş liste dön.
            }
            var submissionKeywords = submission.Keywords.Split(',')
                .Select(k => k.Trim().ToLower())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            if (!submissionKeywords.Any()) return new List<AppUser>();

            // 2. Sistemdeki tüm hakemleri bul.
            var allReviewers = await _userManager.GetUsersInRoleAsync("Reviewer");

            var recommendations = new Dictionary<AppUser, int>();

            // 3. Her bir hakem için eşleşme skoru hesapla.
            foreach (var reviewer in allReviewers)
            {
                if (string.IsNullOrWhiteSpace(reviewer.ExpertiseAreas))
                {
                    continue; // Uzmanlık alanı boş olan hakemi atla.
                }

                var reviewerExpertise = reviewer.ExpertiseAreas.Split(',')
                    .Select(e => e.Trim().ToLower())
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToList();

                // Kesişim kümesini bularak eşleşen kelime sayısını hesapla.
                int score = submissionKeywords.Intersect(reviewerExpertise).Count();

                if (score > 0)
                {
                    recommendations[reviewer] = score;
                }
            }

            // 4. Hakemleri skorlarına göre çoktan aza doğru sırala ve listeyi döndür.
            var sortedRecommendations = recommendations
                .OrderByDescending(pair => pair.Value)
                .Select(pair => pair.Key)
                .ToList();

            return sortedRecommendations;
        }
    }
}