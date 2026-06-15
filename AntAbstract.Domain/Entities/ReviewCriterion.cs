using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    /// <summary>
    /// Bir konferansa ait değerlendirme kriteri.
    /// Admin konferansa özel kriterler tanımlar; hakem her kriter için 0-100 puan verir.
    /// </summary>
    public class ReviewCriterion
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ConferenceId { get; set; }

        [ForeignKey(nameof(ConferenceId))]
        public Conference? Conference { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? NameEn { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>Ağırlık katsayısı (1-10). Ortalama hesabında kullanılır.</summary>
        [Range(1, 10)]
        public int Weight { get; set; } = 1;

        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ReviewCriterionScore> Scores { get; set; } = new List<ReviewCriterionScore>();
    }

    /// <summary>
    /// Bir değerlendirme için kriter bazlı puan.
    /// </summary>
    public class ReviewCriterionScore
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ReviewCriterionId { get; set; }

        [ForeignKey(nameof(ReviewCriterionId))]
        public ReviewCriterion? Criterion { get; set; }

        public int ReviewId { get; set; }

        [ForeignKey(nameof(ReviewId))]
        public Review? Review { get; set; }

        /// <summary>0-100 arası puan.</summary>
        [Range(0, 100)]
        public int Score { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }
    }
}
