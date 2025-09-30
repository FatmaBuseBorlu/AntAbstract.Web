using AntAbstract.Domain.Entities;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public class AssignReviewerViewModel
    {
        public Submission Submission { get; set; }
        public List<AppUser> RecommendedReviewers { get; set; }
        public List<AppUser> AllOtherReviewers { get; set; }
    }
}