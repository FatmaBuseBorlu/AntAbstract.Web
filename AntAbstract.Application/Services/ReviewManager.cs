using AntAbstract.Application.DTOs.Review;
using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AntAbstract.Application.Services
{
    public class ReviewManager : IReviewService
    {
        private readonly IApplicationDbContext _context;

        public ReviewManager(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AssignReviewerAsync(AssignReviewerDto input)
        {
            var existingAssignment = await _context.ReviewAssignments 
                .FirstOrDefaultAsync(ra => ra.SubmissionId == input.SubmissionId && ra.ReviewerId == input.ReviewerId);

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
            var list = await _context.ReviewAssignments
                .Include(ra => ra.Submission).ThenInclude(s => s.Conference)
                .Include(ra => ra.Review)
                .Where(ra => ra.ReviewerId == reviewerId)
                .OrderByDescending(ra => ra.AssignedDate)
                .ToListAsync();

            return list.Select(ra => new ReviewAssignmentDto
            {
                Id = ra.Id,
                AssignedDate = ra.AssignedDate,
                SubmissionTitle = ra.Submission.Title,
                ConferenceName = ra.Submission.Conference?.Title,
                IsReviewed = ra.Review != null,
                Score = ra.Review?.Score
            }).ToList();
        }

        public async Task<ReviewAssignmentDto> GetAssignmentByIdAsync(int id, string reviewerId)
        {
            var assignment = await _context.ReviewAssignments
                .Include(ra => ra.Submission).ThenInclude(s => s.Conference)
                .Include(ra => ra.Submission).ThenInclude(s => s.Files) 
                .Include(ra => ra.Review)
                .FirstOrDefaultAsync(ra => ra.Id == id && ra.ReviewerId == reviewerId);

            if (assignment == null) return null;

            return new ReviewAssignmentDto
            {
                Id = assignment.Id,
                AssignedDate = assignment.AssignedDate,
                SubmissionTitle = assignment.Submission.Title,
                SubmissionAbstract = assignment.Submission.Abstract,
                ConferenceName = assignment.Submission.Conference?.Title,
                Files = assignment.Submission.Files.Select(f => new SubmissionFileDto
                {
                    FileName = f.FileName,
                    FilePath = f.FilePath,
                    Type = f.Type.ToString()
                }).ToList(),

                ReviewId = assignment.Review?.Id,
                CommentsToAuthor = assignment.Review?.CommentsToAuthor,
                Recommendation = assignment.Review?.Recommendation,
                Score = assignment.Review?.Score,
                IsReviewed = assignment.Review != null
            };
        }

        public async Task SubmitReviewAsync(SubmitReviewDto input, string reviewerName)
        {
            var assignment = await _context.ReviewAssignments
                .Include(ra => ra.Review) 
                .FirstOrDefaultAsync(ra => ra.Id == input.ReviewAssignmentId);

            if (assignment == null) throw new Exception("Atama bulunamadı.");

            if (assignment.Review != null)
            {
                throw new Exception("Bu bildiri zaten değerlendirilmiş.");
            }

            var review = new Review
            {
                ReviewAssignmentId = assignment.Id,
                CommentsToAuthor = input.CommentsToAuthor,
                Recommendation = input.Recommendation,
                Score = input.Score,
                ReviewerName = reviewerName, 
                ReviewedAt = DateTime.UtcNow
            };


            await _context.Reviews.AddAsync(review);
            await _context.SaveChangesAsync();
        }
    }
}