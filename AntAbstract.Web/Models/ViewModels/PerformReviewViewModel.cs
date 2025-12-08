using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class PerformReviewViewModel
    {
        public ReviewAssignment Assignment { get; set; }
        public List<ReviewCriterion> Criteria { get; set; }

        public List<ReviewAnswer> Answers { get; set; }
    }
}