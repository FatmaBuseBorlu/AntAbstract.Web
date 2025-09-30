using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AntAbstract.Domain.Entities
{
    public class ReviewCriterion
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(300)]
        public string QuestionText { get; set; }

        [Range(1, 100, ErrorMessage = "Sıra numarası 1 ile 100 arasında olmalıdır.")]
        public int DisplayOrder { get; set; }

        [Required]
        [StringLength(50)]
        public string CriterionType { get; set; } // Örn: "Scale1To10", "Scale1To5", "FreeText"

        public Guid FormId { get; set; }
        [ForeignKey("FormId")]
        [ValidateNever] // ✅ BU SATIRI EKLE
        public ReviewForm Form { get; set; }
    }
}