// AntAbstract.Domain/Entities/ReviewForm.cs
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntAbstract.Domain.Entities
{
    public class ReviewForm
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public Guid ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]

        [ValidateNever]
        public Conference? Conference { get; set; }

        public ICollection<ReviewCriterion> Criteria { get; set; } = new List<ReviewCriterion>();
    }
}