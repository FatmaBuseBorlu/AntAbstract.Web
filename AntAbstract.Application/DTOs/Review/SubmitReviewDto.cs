using System;
using System.Collections.Generic;

namespace AntAbstract.Application.DTOs.Review
{
    public class SubmitReviewDto
    {
        public int ReviewAssignmentId { get; set; }
        public string CommentsToAuthor { get; set; }
        public string Recommendation { get; set; }
        public int Score { get; set; }
        public int ScoreOriginality { get; set; }
        public int ScoreMethodology { get; set; }
        public int ScorePresentation { get; set; }
        public int ScoreRelevance { get; set; }

        /// <summary>
        /// Özel değerlendirme kriterleri: key = ReviewCriterion.Id, value = 0–100 arası puan.
        /// Form'dan Dictionary olarak bind edilir: CustomCriteria[{guid}] = score
        /// </summary>
        public Dictionary<Guid, int>? CustomCriteria { get; set; }
    }
}