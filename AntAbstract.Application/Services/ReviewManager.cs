using AntAbstract.Application.DTOs.Review;
using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Application.Services
{
    public class ReviewManager : IReviewService
    {
        private static readonly HashSet<string> AllowedRecommendations = new HashSet<string>
        {
            "Accept",
            "Reject",
            "MinorRevision",
            "MajorRevision"
        };

        private readonly IApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReviewManager(
            IApplicationDbContext context,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static string NormalizeRecommendation(string? value)
        {
            value = NormalizeText(value);

            if (!AllowedRecommendations.Contains(value))
            {
                throw new Exception("Geçersiz karar önerisi.");
            }

            return value;
        }

        private static int NormalizeScore(int score)
        {
            if (score < 0 || score > 100)
            {
                throw new Exception("Puan 0 ile 100 arasında olmalıdır.");
            }

            return score;
        }

        public async Task AssignReviewerAsync(AssignReviewerDto input)
        {
            if (input.SubmissionId == Guid.Empty)
            {
                throw new Exception("Geçersiz bildiri.");
            }

            if (string.IsNullOrWhiteSpace(input.ReviewerId))
            {
                throw new Exception("Geçersiz hakem.");
            }

            var existingAssignment = await _context.ReviewAssignments
                .FirstOrDefaultAsync(ra =>
                    ra.SubmissionId == input.SubmissionId &&
                    ra.ReviewerId == input.ReviewerId);

            if (existingAssignment != null)
            {
                throw new Exception("Bu hakem bu bildiriye zaten atanmış.");
            }

            var assignment = new ReviewAssignment
            {
                SubmissionId = input.SubmissionId,
                ReviewerId = input.ReviewerId,
                AssignedDate = DateTime.UtcNow
            };

            await _context.ReviewAssignments.AddAsync(assignment);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ReviewAssignmentDto>> GetMyAssignmentsAsync(string reviewerId)
        {
            if (string.IsNullOrWhiteSpace(reviewerId))
            {
                return new List<ReviewAssignmentDto>();
            }

            var assignments = await _context.ReviewAssignments
                .AsNoTracking()
                .Include(ra => ra.Submission)
                    .ThenInclude(s => s.Conference)
                .Include(ra => ra.Review)
                .Where(ra => ra.ReviewerId == reviewerId && !ra.IsDeclined)
                .OrderByDescending(ra => ra.AssignedDate)
                .ToListAsync();

            return assignments.Select(assignment => new ReviewAssignmentDto
            {
                Id = assignment.Id,
                SubmissionId = assignment.SubmissionId,
                AssignedDate = assignment.AssignedDate,

                SubmissionTitle = assignment.Submission?.Title ?? "Başlıksız Bildiri",
                SubmissionAbstract = assignment.Submission?.Abstract ?? string.Empty,
                ConferenceName = assignment.Submission?.Conference?.Title ?? "Kongre Bilgisi Yok",

                IsReviewed = assignment.Review != null,
                ReviewId = assignment.Review?.Id,
                Score = assignment.Review?.Score,
                Recommendation = assignment.Review?.Recommendation ?? string.Empty,
                CommentsToAuthor = assignment.Review?.CommentsToAuthor ?? string.Empty,
                EvaluationDate = assignment.Review?.ReviewedAt,

                ReviewerName = assignment.Review?.ReviewerName ?? string.Empty,
                Files = new List<SubmissionFileDto>(),

                CertificateFirstSignerName = assignment.Submission?.Conference?.CertificateFirstSignerName,
                CertificateFirstSignerTitle = assignment.Submission?.Conference?.CertificateFirstSignerTitle,
                CertificateSecondSignerName = assignment.Submission?.Conference?.CertificateSecondSignerName,
                CertificateSecondSignerTitle = assignment.Submission?.Conference?.CertificateSecondSignerTitle
            }).ToList();
        }

        public async Task<ReviewAssignmentDto> GetAssignmentByIdAsync(
            int id,
            string reviewerId)
        {
            if (id <= 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(reviewerId))
            {
                return null;
            }

            var assignment = await _context.ReviewAssignments
                .AsNoTracking()
                .Include(ra => ra.Submission)
                    .ThenInclude(s => s.Conference)
                .Include(ra => ra.Submission)
                    .ThenInclude(s => s.Files)
                .Include(ra => ra.Review)
                .FirstOrDefaultAsync(ra =>
                    ra.Id == id &&
                    ra.ReviewerId == reviewerId);

            if (assignment == null)
            {
                return null;
            }

            return new ReviewAssignmentDto
            {
                Id = assignment.Id,
                SubmissionId = assignment.SubmissionId,
                AssignedDate = assignment.AssignedDate,

                SubmissionTitle = assignment.Submission?.Title ?? "Başlıksız Bildiri",
                SubmissionAbstract = assignment.Submission?.Abstract ?? string.Empty,
                ConferenceName = assignment.Submission?.Conference?.Title ?? "Kongre Bilgisi Yok",

                Files = assignment.Submission?.Files?
                    .Select(file => new SubmissionFileDto
                    {
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        Type = file.Type.ToString()
                    })
                    .ToList() ?? new List<SubmissionFileDto>(),

                ReviewId = assignment.Review?.Id,
                CommentsToAuthor = assignment.Review?.CommentsToAuthor ?? string.Empty,
                Recommendation = assignment.Review?.Recommendation ?? string.Empty,
                Score = assignment.Review?.Score,
                IsReviewed = assignment.Review != null,
                EvaluationDate = assignment.Review?.ReviewedAt,
                ReviewerName = assignment.Review?.ReviewerName ?? string.Empty,

                CertificateFirstSignerName = assignment.Submission?.Conference?.CertificateFirstSignerName,
                CertificateFirstSignerTitle = assignment.Submission?.Conference?.CertificateFirstSignerTitle,
                CertificateSecondSignerName = assignment.Submission?.Conference?.CertificateSecondSignerName,
                CertificateSecondSignerTitle = assignment.Submission?.Conference?.CertificateSecondSignerTitle
            };
        }

        public async Task SubmitReviewAsync(
            SubmitReviewDto input,
            string reviewerName)
        {
            if (input.ReviewAssignmentId <= 0)
            {
                throw new Exception("Geçersiz değerlendirme görevi.");
            }

            var score = NormalizeScore(input.Score);
            var recommendation = NormalizeRecommendation(input.Recommendation);
            var commentsToAuthor = NormalizeText(input.CommentsToAuthor);

            if (string.IsNullOrWhiteSpace(commentsToAuthor))
            {
                throw new Exception("Yazara iletilecek değerlendirme notu boş olamaz.");
            }

            reviewerName = NormalizeText(reviewerName);

            if (string.IsNullOrWhiteSpace(reviewerName))
            {
                reviewerName = "Hakem";
            }

            var assignment = await _context.ReviewAssignments
                .Include(ra => ra.Review)
                .FirstOrDefaultAsync(ra => ra.Id == input.ReviewAssignmentId);

            if (assignment == null)
            {
                throw new Exception("Atama bulunamadı.");
            }

            if (assignment.Review != null)
            {
                throw new Exception("Bu bildiri zaten değerlendirilmiş.");
            }

            var review = new Review
            {
                ReviewAssignmentId = assignment.Id,
                CommentsToAuthor = commentsToAuthor,
                Recommendation = recommendation,
                Score = score,
                ScoreOriginality   = Math.Max(0, Math.Min(100, input.ScoreOriginality)),
                ScoreMethodology   = Math.Max(0, Math.Min(100, input.ScoreMethodology)),
                ScorePresentation  = Math.Max(0, Math.Min(100, input.ScorePresentation)),
                ScoreRelevance     = Math.Max(0, Math.Min(100, input.ScoreRelevance)),
                ReviewerName = reviewerName,
                ReviewedAt = DateTime.UtcNow
            };

            await _context.Reviews.AddAsync(review);
            await _context.SaveChangesAsync();

            // Özel değerlendirme kriterleri varsa kaydet
            if (input.CustomCriteria != null && input.CustomCriteria.Count > 0)
            {
                foreach (var kv in input.CustomCriteria)
                {
                    var score100 = Math.Max(0, Math.Min(100, kv.Value));
                    _context.ReviewCriterionScores.Add(new AntAbstract.Domain.Entities.ReviewCriterionScore
                    {
                        ReviewCriterionId = kv.Key,
                        ReviewId = review.Id,
                        Score = score100
                    });
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeclineAssignmentAsync(
            int id,
            string userId,
            string reason,
            string note)
        {
            if (id <= 0)
            {
                throw new Exception("Geçersiz değerlendirme görevi.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new Exception("Geçersiz kullanıcı.");
            }

            var assignment = await _context.ReviewAssignments
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.ReviewerId == userId);

            if (assignment == null)
            {
                throw new Exception("Değerlendirme görevi bulunamadı.");
            }

            // Soft-delete: kaydı silmek yerine ret olarak işaretle
            assignment.IsDeclined  = true;
            assignment.DeclineReason = string.IsNullOrWhiteSpace(reason) ? note : reason;
            assignment.DeclinedAt  = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<List<ReviewAssignmentDto>> GetReviewsBySubmissionIdAsync(Guid submissionId)
        {
            if (submissionId == Guid.Empty)
            {
                return new List<ReviewAssignmentDto>();
            }

            var assignments = await _context.ReviewAssignments
                .AsNoTracking()
                .Include(ra => ra.Review)
                .Where(ra => ra.SubmissionId == submissionId)
                .ToListAsync();

            var resultList = new List<ReviewAssignmentDto>();

            foreach (var assignment in assignments)
            {
                string reviewerName = "Bilinmiyor";

                if (assignment.Review != null &&
                    !string.IsNullOrWhiteSpace(assignment.Review.ReviewerName))
                {
                    reviewerName = assignment.Review.ReviewerName;
                }
                else
                {
                    var user = await _userManager.FindByIdAsync(assignment.ReviewerId);

                    if (user != null)
                    {
                        reviewerName = $"{user.FirstName} {user.LastName}".Trim();

                        if (string.IsNullOrWhiteSpace(reviewerName))
                        {
                            reviewerName = user.UserName ?? user.Email ?? "Bilinmiyor";
                        }
                    }
                }

                resultList.Add(new ReviewAssignmentDto
                {
                    Id = assignment.Id,
                    SubmissionId = assignment.SubmissionId,
                    AssignedDate = assignment.AssignedDate,
                    IsReviewed = assignment.Review != null,
                    IsDeclined = assignment.IsDeclined,
                    DeclineReason = assignment.DeclineReason,
                    DeclinedAt = assignment.DeclinedAt,
                    ReviewerName = reviewerName,
                    Score = assignment.Review?.Score,
                    Recommendation = assignment.Review?.Recommendation ?? string.Empty,
                    CommentsToAuthor = assignment.Review?.CommentsToAuthor ?? string.Empty,
                    EvaluationDate = assignment.Review?.ReviewedAt
                });
            }

            return resultList;
        }
    }
}