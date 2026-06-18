using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AntAbstract.Domain.Entities
{
    public class Review
    {
        [Key]
        public int Id { get; set; }

        public int ReviewAssignmentId { get; set; }
        public ReviewAssignment ReviewAssignment { get; set; } = null!;

        public string ReviewerName { get; set; } = string.Empty;
        public string CommentsToAuthor { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public int Score { get; set; }

        // Alt kriter puanları (0 = girilmemiş)
        public int ScoreOriginality { get; set; }       // Özgünlük
        public int ScoreMethodology { get; set; }       // Metodoloji
        public int ScorePresentation { get; set; }      // Sunum / Yazım
        public int ScoreRelevance { get; set; }         // Konu uygunluğu

        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Konferansa özel kriter puanları (opsiyonel).</summary>
        public ICollection<ReviewCriterionScore> CriterionScores { get; set; } = new List<ReviewCriterionScore>();
    }
}
