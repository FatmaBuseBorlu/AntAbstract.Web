using AntAbstract.Domain.Entities;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AntAbstract.Web.Models.ViewModels
{
    public class ManageCriteriaViewModel
    {
        public ReviewForm Form { get; set; }
        public List<ReviewCriterion> ExistingCriteria { get; set; }
        public ReviewCriterion NewCriterion { get; set; }
        public List<SelectListItem> CriterionTypes { get; set; }
    }
}