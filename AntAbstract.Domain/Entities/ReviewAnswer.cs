using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;

namespace AntAbstract.Domain.Entities
{
    public class ReviewAnswer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ReviewId { get; set; }
        [ForeignKey("ReviewId")]
        public Review Review { get; set; }

        public Guid CriterionId { get; set; }
        [ForeignKey("CriterionId")]
        public ReviewCriterion Criterion { get; set; }

        public int? Score { get; set; }

        public string? TextAnswer { get; set; }
    }
}